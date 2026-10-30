using Mizan.Prices.Scraping;

namespace Mizan.Prices.Tests.Support;

/// <summary>Waits take no wall time: a delay moves the clock forward and is written down.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset start)
    {
        UtcNow = start;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan duration, CancellationToken ct)
    {
        Delays.Add(duration);
        UtcNow += duration;
        return Task.CompletedTask;
    }

    public void Advance(TimeSpan by) => UtcNow += by;
}
