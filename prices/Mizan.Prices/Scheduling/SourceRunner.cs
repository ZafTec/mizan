using Microsoft.Extensions.Logging;
using Mizan.Contracts.Prices;
using Mizan.Prices.Backend;
using Mizan.Prices.Scraping;

namespace Mizan.Prices.Scheduling;

/// <summary>What one run came to. Also the body of the worker's own "Run now" answer.</summary>
public sealed record RunOutcome(Guid SourceId, string Status, int Observations, int Stored, int Queued, int Ignored, string? Error);

/// <summary>
/// One source, start to finish: take the lock, scrape, post observations in
/// chunks as they arrive, report the run. Every failure ends as a reported
/// run, never as an exception to the caller, so one broken site cannot stop
/// the schedule for the others.
/// </summary>
public sealed class SourceRunner
{
    public static readonly TimeSpan LockTtl = TimeSpan.FromHours(1);

    private readonly IPricesApi _api;
    private readonly ScraperRegistry _registry;
    private readonly ScrapeContextFactory _contexts;
    private readonly IRunLock _lock;
    private readonly IClock _clock;
    private readonly ILogger<SourceRunner> _logger;

    public SourceRunner(
        IPricesApi api,
        ScraperRegistry registry,
        ScrapeContextFactory contexts,
        IRunLock runLock,
        IClock clock,
        ILogger<SourceRunner> logger)
    {
        _api = api;
        _registry = registry;
        _contexts = contexts;
        _lock = runLock;
        _clock = clock;
        _logger = logger;
    }

    public static string LockKey(Guid sourceId) => $"prices:run:{sourceId}";

    /// <summary>Runs with the crawl settings read from the API now.</summary>
    public async Task<RunOutcome> RunAsync(PriceSourceDto source, CancellationToken ct) =>
        await RunAsync(source, null, ct);

    /// <summary>
    /// Runs with <paramref name="settings"/>, as sent with a Run now request or read once per scheduler
    /// tick, or with the API's current ones when null.
    /// </summary>
    public async Task<RunOutcome> RunAsync(PriceSourceDto source, PriceCrawlSettings? settings, CancellationToken ct)
    {
        var scraper = _registry.Find(source.ScraperKey);
        if (scraper is null)
        {
            return await ReportAsync(source, PriceRunStatus.Failed, 0, new PriceObservationReceipt(0, 0, 0),
                $"No scraper is registered for key '{source.ScraperKey}'", ct);
        }

        IAsyncDisposable? lease;
        try
        {
            lease = await _lock.TryAcquireAsync(LockKey(source.Id), LockTtl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Without the lock we cannot know whether another instance is on
            // this source. Not running is the safe answer.
            return await ReportAsync(source, PriceRunStatus.Failed, 0, new PriceObservationReceipt(0, 0, 0),
                $"Run lock unavailable: {ex.Message}", ct);
        }

        if (lease is null)
        {
            _logger.LogInformation("{Source}: another worker holds the run lock; skipped", source.Name);
            return await ReportAsync(source, PriceRunStatus.Skipped, 0, new PriceObservationReceipt(0, 0, 0),
                "Another worker holds the run lock", ct);
        }

        await using (lease)
        {
            var count = 0;
            var receipt = new PriceObservationReceipt(0, 0, 0);
            var buffer = new List<PriceObservation>(PricesApiClient.ChunkSize);

            try
            {
                _logger.LogInformation("{Source}: run started with scraper {Key}", source.Name, scraper.Key);
                var ctx = _contexts.Create(settings ?? await _api.GetCrawlSettingsAsync(ct));

                await foreach (var observation in scraper.ScrapeAsync(source, ctx, ct).WithCancellation(ct))
                {
                    buffer.Add(observation);
                    count++;

                    if (buffer.Count >= PricesApiClient.ChunkSize)
                    {
                        receipt = Add(receipt, await FlushAsync(source, buffer, ct));
                    }
                }

                if (buffer.Count > 0) receipt = Add(receipt, await FlushAsync(source, buffer, ct));

                _logger.LogInformation("{Source}: {Count} observations ({Stored} stored, {Queued} queued, {Ignored} ignored)",
                    source.Name, count, receipt.Stored, receipt.Queued, receipt.Ignored);
                return await ReportAsync(source, PriceRunStatus.Succeeded, count, receipt, null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Source}: run failed after {Count} observations", source.Name, count);
                return await ReportAsync(source, PriceRunStatus.Failed, count, receipt, Describe(ex), ct);
            }
        }
    }

    private async Task<PriceObservationReceipt> FlushAsync(PriceSourceDto source, List<PriceObservation> buffer, CancellationToken ct)
    {
        var batch = new PriceObservationBatch(source.Id, buffer.ToList());
        buffer.Clear();
        return await _api.PostObservationsAsync(batch, ct);
    }

    private async Task<RunOutcome> ReportAsync(
        PriceSourceDto source, string status, int observations, PriceObservationReceipt receipt, string? error, CancellationToken ct)
    {
        var result = new PriceSourceRunResult(status, error, observations, _clock.UtcNow.UtcDateTime);

        try
        {
            await _api.PostRunAsync(source.Id, result, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The observations are already stored; only the run record is
            // lost. The next tick sees LastRunAt unchanged and tries again.
            _logger.LogWarning(ex, "{Source}: could not report the {Status} run", source.Name, status);
        }

        return new RunOutcome(source.Id, status, observations, receipt.Stored, receipt.Queued, receipt.Ignored, error);
    }

    private static PriceObservationReceipt Add(PriceObservationReceipt a, PriceObservationReceipt b) =>
        new(a.Stored + b.Stored, a.Queued + b.Queued, a.Ignored + b.Ignored);

    private static string Describe(Exception ex)
    {
        var message = ex.InnerException is null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";
        return message.Length <= 1000 ? message : message[..1000];
    }
}
