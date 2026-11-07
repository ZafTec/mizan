namespace Mizan.Prices.Scraping;

/// <summary>
/// Time, for the pacing and the schedule. One seam so tests can wait zero
/// seconds and still see that a wait was requested.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    Task DelayAsync(TimeSpan duration, CancellationToken ct);
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public Task DelayAsync(TimeSpan duration, CancellationToken ct) =>
        duration <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(duration, ct);
}
