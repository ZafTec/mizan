using Xunit;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Prices;
using Mizan.Prices.Backend;
using Mizan.Prices.Scheduling;
using Mizan.Prices.Scraping;
using Mizan.Prices.Tests.Support;

namespace Mizan.Prices.Tests;

public class SourceRunnerTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    private static readonly PriceSourceDto Arada = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Arada Mart", "Scraper", "https://www.aradamart.net", "fake", "ET-AA", "ETB", "0 3 * * *", null);

    private readonly FakePricesApi _api = new();
    private readonly FakeScraper _scraper = new();
    private readonly InMemoryRunLock _lock = new(() => Now);

    private SourceRunner Runner() => new(
        _api,
        new ScraperRegistry([_scraper]),
        new ScrapeContextFactory(
            new SingleClientFactory(new FakeHttpMessageHandler().CreateClient()),
            Options.Create(new PricesOptions()),
            new FakeClock(Now),
            NullLoggerFactory.Instance),
        _lock,
        new FakeClock(Now),
        NullLogger<SourceRunner>.Instance);

    [Fact]
    public async Task ARun_PostsObservationsInChunksOfAHundred_ThenReportsSuccess()
    {
        _scraper.Observations = Enumerable.Range(1, 250).Select(Observation).ToList();

        var outcome = await Runner().RunAsync(Arada, CancellationToken.None);

        outcome.Status.Should().Be(PriceRunStatus.Succeeded);
        outcome.Observations.Should().Be(250);
        _api.Batches.Select(b => b.Observations.Count).Should().Equal(100, 100, 50);
        _api.Batches.Should().OnlyContain(b => b.SourceId == Arada.Id);
        _api.Runs.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (Arada.Id, new PriceSourceRunResult(PriceRunStatus.Succeeded, null, 250, Now)));
        // Stored, queued and ignored are summed across the chunks the API answered.
        outcome.Stored.Should().Be(250);
        _lock.IsHeld(SourceRunner.LockKey(Arada.Id)).Should().BeFalse("the lease is released when the run ends");
    }

    [Fact]
    public async Task WhenAnotherWorkerHoldsTheLock_TheRunIsSkipped_AndNothingIsScraped()
    {
        await using var held = await _lock.TryAcquireAsync(SourceRunner.LockKey(Arada.Id), TimeSpan.FromHours(1), CancellationToken.None);
        _scraper.Observations = [Observation(1)];

        var outcome = await Runner().RunAsync(Arada, CancellationToken.None);

        outcome.Status.Should().Be(PriceRunStatus.Skipped);
        _scraper.Calls.Should().Be(0);
        _api.Batches.Should().BeEmpty();
        _api.Runs.Should().ContainSingle().Which.Result.Status.Should().Be(PriceRunStatus.Skipped);
    }

    [Fact]
    public async Task OnceTheLockIsReleased_TheNextRunGoesAhead()
    {
        var held = await _lock.TryAcquireAsync(SourceRunner.LockKey(Arada.Id), TimeSpan.FromHours(1), CancellationToken.None);
        _scraper.Observations = [Observation(1)];

        (await Runner().RunAsync(Arada, CancellationToken.None)).Status.Should().Be(PriceRunStatus.Skipped);
        await held!.DisposeAsync();
        (await Runner().RunAsync(Arada, CancellationToken.None)).Status.Should().Be(PriceRunStatus.Succeeded);

        _scraper.Calls.Should().Be(1);
    }

    [Fact]
    public async Task AScraperThatThrowsHalfway_ReportsAFailedRun_AndKeepsWhatWasAlreadyPosted()
    {
        _scraper.Observations = Enumerable.Range(1, 120).Select(Observation).ToList();
        _scraper.ThrowAfter = 120;
        _scraper.Error = new HttpRequestException("www.aradamart.net did not answer within 30 s");

        var outcome = await Runner().RunAsync(Arada, CancellationToken.None);

        outcome.Status.Should().Be(PriceRunStatus.Failed);
        outcome.Error.Should().Be("www.aradamart.net did not answer within 30 s");
        outcome.Observations.Should().Be(120);
        // The first chunk went out before the failure; the second was never completed.
        _api.Batches.Select(b => b.Observations.Count).Should().Equal(100);
        _api.Runs.Should().ContainSingle().Which.Result.Should().BeEquivalentTo(
            new PriceSourceRunResult(PriceRunStatus.Failed, "www.aradamart.net did not answer within 30 s", 120, Now));
        _lock.IsHeld(SourceRunner.LockKey(Arada.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task AnUnknownScraperKey_IsAFailedRun_NotAnException()
    {
        var outcome = await Runner().RunAsync(Arada with { ScraperKey = "asbeza" }, CancellationToken.None);

        outcome.Status.Should().Be(PriceRunStatus.Failed);
        outcome.Error.Should().Contain("asbeza");
        _api.Runs.Should().ContainSingle();
    }

    [Fact]
    public async Task AnApiThatRejectsTheBatch_FailsTheRun_WithTheReason()
    {
        _scraper.Observations = [Observation(1)];
        _api.RejectBatches = new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized).");

        var outcome = await Runner().RunAsync(Arada, CancellationToken.None);

        outcome.Status.Should().Be(PriceRunStatus.Failed);
        outcome.Error.Should().Contain("401");
    }

    [Fact]
    public void TheScheduler_RunsScraperSourcesWithARegisteredKey_WhoseCronIsDue()
    {
        var registry = new ScraperRegistry([_scraper]);
        var sources = new[]
        {
            Arada with { Name = "due, never ran" },
            Arada with { Id = Guid.NewGuid(), Name = "ran today", LastRunAt = Now.AddHours(-1) },
            Arada with { Id = Guid.NewGuid(), Name = "unknown key", ScraperKey = "asbeza" },
            Arada with { Id = Guid.NewGuid(), Name = "manual source", Kind = "Manual" },
            Arada with { Id = Guid.NewGuid(), Name = "no schedule", Schedule = null },
            Arada with { Id = Guid.NewGuid(), Name = "due, ran yesterday", LastRunAt = Now.AddDays(-1) },
        };

        var due = ScrapeScheduler.SelectDue(sources, registry, Now);

        due.Select(s => s.Name).Should().Equal("due, never ran", "due, ran yesterday");
    }

    private static PriceObservation Observation(int i) =>
        new($"Product {i}", $"sku-{i}", 10m + i, "ETB", null, null, Now, $"https://www.aradamart.net/product-page/{i}");

    private sealed class FakeScraper : IPriceScraper
    {
        public string Key => "fake";
        public List<PriceObservation> Observations { get; set; } = [];
        public int ThrowAfter { get; set; } = int.MaxValue;
        public Exception Error { get; set; } = new InvalidOperationException("boom");
        public int Calls { get; private set; }

        public async IAsyncEnumerable<PriceObservation> ScrapeAsync(
            PriceSourceDto source, ScrapeContext ctx, [EnumeratorCancellation] CancellationToken ct)
        {
            Calls++;
            var emitted = 0;
            foreach (var observation in Observations)
            {
                await Task.Yield();
                yield return observation;
                if (++emitted >= ThrowAfter) throw Error;
            }
        }
    }

    private sealed class FakePricesApi : IPricesApi
    {
        public List<PriceSourceDto> Sources { get; } = [];
        public List<PriceObservationBatch> Batches { get; } = [];
        public List<(Guid SourceId, PriceSourceRunResult Result)> Runs { get; } = [];
        public Exception? RejectBatches { get; set; }

        public Task<IReadOnlyList<PriceSourceDto>> GetSourcesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PriceSourceDto>>(Sources);

        public Task<PriceObservationReceipt> PostObservationsAsync(PriceObservationBatch batch, CancellationToken ct)
        {
            if (RejectBatches is not null) throw RejectBatches;
            Batches.Add(batch);
            return Task.FromResult(new PriceObservationReceipt(batch.Observations.Count, 0, 0));
        }

        public Task PostRunAsync(Guid sourceId, PriceSourceRunResult result, CancellationToken ct)
        {
            Runs.Add((sourceId, result));
            return Task.CompletedTask;
        }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
