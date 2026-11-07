using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Prices;
using Mizan.Prices.Backend;
using Mizan.Prices.Scraping;

namespace Mizan.Prices.Scheduling;

/// <summary>
/// Once a minute: ask the API for the enabled scraper sources, run the ones
/// whose cron is due. The API's <c>LastRunAt</c> is the only clock state; the
/// worker remembers nothing between ticks, so a restart changes nothing.
/// </summary>
public sealed class ScrapeScheduler : BackgroundService
{
    public static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ScraperRegistry _registry;
    private readonly IOptions<PricesOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<ScrapeScheduler> _logger;

    public ScrapeScheduler(
        IServiceScopeFactory scopes,
        ScraperRegistry registry,
        IOptions<PricesOptions> options,
        IClock clock,
        ILogger<ScrapeScheduler> logger)
    {
        _scopes = scopes;
        _registry = registry;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// The sources this instance should run now: scraper kind, a registered
    /// key, and a cron that has come due since the last run. Pure, so the
    /// tests can table it.
    /// </summary>
    public static IReadOnlyList<PriceSourceDto> SelectDue(
        IEnumerable<PriceSourceDto> sources, ScraperRegistry registry, DateTime nowUtc, ILogger? logger = null)
    {
        var due = new List<PriceSourceDto>();

        foreach (var source in sources)
        {
            if (!string.Equals(source.Kind, "Scraper", StringComparison.OrdinalIgnoreCase)) continue;

            if (registry.Find(source.ScraperKey) is null)
            {
                logger?.LogWarning("{Source}: no scraper registered for key '{Key}'", source.Name, source.ScraperKey);
                continue;
            }

            if (!CronSchedule.TryIsDue(source.Schedule, source.LastRunAt, nowUtc, out var isDue))
            {
                logger?.LogWarning("{Source}: schedule '{Schedule}' is missing or invalid; manual runs only", source.Name, source.Schedule);
                continue;
            }

            if (isDue) due.Add(source);
        }

        return due;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.IsConfigured)
        {
            _logger.LogWarning("Prices worker is not configured (Prices:ApiUrl and Prices:ServiceApiKey); the scheduler is idle");
            return;
        }

        using var timer = new PeriodicTimer(Tick);

        do
        {
            try
            {
                await RunTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Usually the API being down. The next tick asks again.
                _logger.LogWarning(ex, "Scheduler tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IPricesApi>();
        var runner = scope.ServiceProvider.GetRequiredService<SourceRunner>();

        var sources = await api.GetSourcesAsync(ct);
        var due = SelectDue(sources, _registry, _clock.UtcNow.UtcDateTime, _logger);
        if (due.Count == 0) return;

        // Read once per tick, so a change in the admin area applies from the next tick on.
        var settings = await api.GetCrawlSettingsAsync(ct);

        foreach (var source in due)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await runner.RunAsync(source, settings, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // RunAsync reports its own failures; this is the belt for its braces.
                _logger.LogError(ex, "{Source}: run threw past the runner", source.Name);
            }
        }
    }
}
