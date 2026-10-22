using Cronos;

namespace Mizan.Prices.Scheduling;

/// <summary>
/// Five-field cron, read in UTC. A source is due when an occurrence has
/// passed since its last run, or when it has never run.
/// </summary>
public static class CronSchedule
{
    /// <summary>False when the schedule is missing or does not parse; <paramref name="due"/> is then false too.</summary>
    public static bool TryIsDue(string? schedule, DateTime? lastRunAt, DateTime nowUtc, out bool due)
    {
        due = false;
        if (string.IsNullOrWhiteSpace(schedule)) return false;

        CronExpression expression;
        try
        {
            expression = CronExpression.Parse(schedule.Trim(), CronFormat.Standard);
        }
        catch (CronFormatException)
        {
            return false;
        }

        if (lastRunAt is null)
        {
            due = true;
            return true;
        }

        var from = DateTime.SpecifyKind(lastRunAt.Value, DateTimeKind.Utc);
        var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var next = expression.GetNextOccurrence(from, inclusive: false);

        due = next is not null && next.Value <= now;
        return true;
    }
}
