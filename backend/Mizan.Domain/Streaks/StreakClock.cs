namespace Mizan.Domain.Streaks;

/// <summary>
/// A streak as it stands right now, rather than as it was last written.
/// </summary>
/// <param name="CurrentCount">Zero once the streak has lapsed, whatever the stored row still says.</param>
/// <param name="IsActiveToday">Whether today has already been logged.</param>
/// <param name="Today">The user's local date, which is what a "day" means here.</param>
/// <param name="ResetsAt">Local midnight, as an instant. What the UI counts down to.</param>
/// <param name="AtRisk">Yesterday was the last activity: one more missed day and only a freeze saves it.</param>
public readonly record struct StreakState(
    int CurrentCount,
    int LongestCount,
    bool IsActiveToday,
    DateOnly Today,
    DateTimeOffset ResetsAt,
    int FreezesAvailable,
    bool AtRisk);

/// <summary>
/// One definition of "what day is it for this user" and "is this streak still
/// alive", shared by the writer and every reader.
///
/// This exists because there were three readers of <c>Streak.CurrentCount</c>
/// and only one of them knew the decay rule. The other two showed a lapsed
/// streak at its old length - and one of them awarded achievements off it, so
/// a dead 30-day streak could still unlock a 30-day badge.
///
/// Days are the user's local days. UTC days mean a user at UTC+3 logging a
/// late-night snack records it against yesterday, and their streak never
/// advances no matter how consistent they are.
/// </summary>
public static class StreakClock
{
    public const string DefaultTimeZone = "UTC";

    /// <summary>
    /// A missed day is survivable with a freeze; two is not. This is the whole
    /// rule, in one place, so the writer and the readers cannot disagree.
    /// </summary>
    private const int GraceDays = 2;

    public static TimeZoneInfo Zone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;

        // A stored zone can go stale - a database restored onto a host with a
        // different tzdata, or a hand-edited value. UTC is wrong but it is not
        // broken, and it is better than throwing on the logging path.
        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
            ? zone
            : TimeZoneInfo.Utc;
    }

    public static bool IsKnownZone(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId)
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);

    public static DateOnly Today(string? timeZoneId, DateTimeOffset utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, Zone(timeZoneId)).DateTime);

    /// <summary>
    /// The next local midnight, as an instant. Computed from the zone's own
    /// offset at that moment, so a day that starts before a DST change and
    /// ends after it still ends at midnight rather than 23:00 or 01:00.
    /// </summary>
    public static DateTimeOffset ResetsAt(string? timeZoneId, DateTimeOffset utcNow)
    {
        var zone = Zone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, zone);
        var midnight = local.Date.AddDays(1);
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
    }

    public static StreakState Evaluate(
        int currentCount,
        int longestCount,
        DateOnly? lastActivityDate,
        int freezesAvailable,
        string? timeZoneId,
        DateTimeOffset utcNow)
    {
        var today = Today(timeZoneId, utcNow);
        var resetsAt = ResetsAt(timeZoneId, utcNow);

        if (lastActivityDate is not { } last)
        {
            return new StreakState(0, longestCount, false, today, resetsAt, freezesAvailable, false);
        }

        var elapsed = today.DayNumber - last.DayNumber;

        // A future last-activity date means the user crossed a timezone
        // westward. Their streak is intact; today simply already counts.
        if (elapsed < 0)
        {
            return new StreakState(currentCount, longestCount, true, today, resetsAt, freezesAvailable, false);
        }

        var alive = elapsed < GraceDays || (elapsed == GraceDays && freezesAvailable > 0);

        return new StreakState(
            alive ? currentCount : 0,
            longestCount,
            elapsed == 0,
            today,
            resetsAt,
            freezesAvailable,
            alive && elapsed >= 1);
    }

    /// <summary>
    /// What recording activity on <paramref name="today"/> does to a streak.
    /// Pure, so the interesting cases - a gap, a freeze, a same-day repeat -
    /// are testable without a database.
    /// </summary>
    public static StreakTransition Extend(
        int currentCount,
        int longestCount,
        DateOnly? lastActivityDate,
        int freezesAvailable,
        DateOnly today)
    {
        if (lastActivityDate == today)
        {
            return new StreakTransition(currentCount, longestCount, freezesAvailable, false, false, false);
        }

        var elapsed = lastActivityDate is { } last ? today.DayNumber - last.DayNumber : int.MaxValue;
        var freezeConsumed = elapsed == GraceDays && freezesAvailable > 0;
        var continues = elapsed == 1 || freezeConsumed;

        var count = continues ? currentCount + 1 : 1;
        var freezes = freezeConsumed ? freezesAvailable - 1 : freezesAvailable;

        // A freeze every seven days, capped at two. Earned on the day the
        // streak reaches the multiple, so it is available for the next gap.
        if (count % 7 == 0) freezes = Math.Min(2, freezes + 1);

        return new StreakTransition(
            count,
            Math.Max(longestCount, count),
            freezes,
            Extended: true,
            IsNewRecord: count > longestCount,
            FreezeConsumed: freezeConsumed);
    }
}

public readonly record struct StreakTransition(
    int CurrentCount,
    int LongestCount,
    int FreezesAvailable,
    bool Extended,
    bool IsNewRecord,
    bool FreezeConsumed);
