using System.Globalization;

namespace Mizan.Domain.Admin;

public enum SystemHealthStatus
{
    Ok,
    Degraded,
    Down,

    /// <summary>Nothing is set up to check. Shown as such, never as a green tile.</summary>
    NotConfigured,
}

public sealed record HealthVerdict(SystemHealthStatus Status, string Detail);

/// <summary>
/// What each measurement on the system health page means. The probes collect
/// numbers; these rules decide the colour, so the thresholds live in one place
/// and a test can read them as a table.
/// </summary>
public static class SystemHealthRules
{
    /// <summary>A round trip slower than this is worth a look even though it worked.</summary>
    public static readonly TimeSpan SlowRoundTrip = TimeSpan.FromSeconds(1);

    /// <summary>Pending work older than this means the dispatcher is behind.</summary>
    public static readonly TimeSpan OutboxBehind = TimeSpan.FromMinutes(10);

    /// <summary>Pending work older than this means the dispatcher has stopped.</summary>
    public static readonly TimeSpan OutboxStalled = TimeSpan.FromHours(1);

    /// <summary>The share of a daily AI ceiling at which the tile turns amber.</summary>
    public const decimal AiWarnShare = 0.8m;

    /// <summary>A scraper that has not succeeded for this long has fallen behind its daily schedule.</summary>
    public static readonly TimeSpan PricesStale = TimeSpan.FromHours(48);

    public static HealthVerdict RoundTrip(bool configured, TimeSpan? elapsed, string? error)
    {
        if (!configured) return new(SystemHealthStatus.NotConfigured, "No connection is configured.");
        if (elapsed is null) return new(SystemHealthStatus.Down, error ?? "No answer.");
        var ms = $"{elapsed.Value.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)} ms round trip";
        return elapsed.Value > SlowRoundTrip ? new(SystemHealthStatus.Degraded, $"Slow: {ms}") : new(SystemHealthStatus.Ok, ms);
    }

    public static HealthVerdict Outbox(int pending, int deadLettered, TimeSpan? oldestPending)
    {
        var detail = $"{pending} pending, {deadLettered} failed";
        if (oldestPending is { } age && pending > 0) detail += $", oldest {Age(age)}";

        if (oldestPending > OutboxStalled) return new(SystemHealthStatus.Down, detail);
        if (deadLettered > 0 || oldestPending > OutboxBehind) return new(SystemHealthStatus.Degraded, detail);
        return new(SystemHealthStatus.Ok, detail);
    }

    public static HealthVerdict Email(bool configured, int failedLastDay)
    {
        if (!configured) return new(SystemHealthStatus.NotConfigured, "No SMTP host is set.");
        var detail = failedLastDay == 1 ? "1 failed in the last 24 hours" : $"{failedLastDay} failed in the last 24 hours";
        return failedLastDay > 0 ? new(SystemHealthStatus.Degraded, detail) : new(SystemHealthStatus.Ok, detail);
    }

    public static HealthVerdict Ai(bool configured, long tokensToday, long tokenCeiling, long costMicrosToday, long costCeilingMicros)
    {
        if (!configured) return new(SystemHealthStatus.NotConfigured, "No AI provider is set.");

        var detail = string.Create(
            CultureInfo.InvariantCulture,
            $"{tokensToday:N0} of {tokenCeiling:N0} tokens, ${costMicrosToday / 1_000_000m:0.00} of ${costCeilingMicros / 1_000_000m:0.00} today");
        var share = Math.Max(Share(tokensToday, tokenCeiling), Share(costMicrosToday, costCeilingMicros));

        if (share >= 1m) return new(SystemHealthStatus.Down, $"Ceiling reached: {detail}");
        if (share >= AiWarnShare) return new(SystemHealthStatus.Degraded, detail);
        return new(SystemHealthStatus.Ok, detail);
    }

    public static HealthVerdict Reachable(bool configured, bool? reachable, string service)
    {
        if (!configured) return new(SystemHealthStatus.NotConfigured, $"No {service} address is set.");
        return reachable == true
            ? new(SystemHealthStatus.Ok, $"The {service} answered.")
            : new(SystemHealthStatus.Down, $"The {service} did not answer.");
    }

    public static HealthVerdict Configured(bool configured, string what) =>
        configured ? new(SystemHealthStatus.Ok, $"{what} is configured.") : new(SystemHealthStatus.NotConfigured, $"{what} is not configured.");

    public static HealthVerdict Prices(bool configured, DateTime? lastSuccess, DateTime now)
    {
        if (!configured) return new(SystemHealthStatus.NotConfigured, "No prices worker is set.");
        if (lastSuccess is null) return new(SystemHealthStatus.Degraded, "No source has run successfully yet.");

        var age = now - lastSuccess.Value;
        var detail = $"Last successful run {Age(age)} ago";
        return age > PricesStale ? new(SystemHealthStatus.Degraded, detail) : new(SystemHealthStatus.Ok, detail);
    }

    private static decimal Share(long used, long ceiling) => ceiling <= 0 ? 0m : (decimal)used / ceiling;

    private static string Age(TimeSpan age) =>
        age.TotalMinutes < 1 ? "under a minute"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min"
        : age.TotalDays < 2 ? $"{(int)age.TotalHours} h"
        : $"{(int)age.TotalDays} days";
}
