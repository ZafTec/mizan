namespace Mizan.Domain.Notifications;

public enum NotificationTriggerKind
{
    /// <summary>Once, at <see cref="NotificationTrigger.At"/> (UTC).</summary>
    Once = 0,

    /// <summary>Every day at <see cref="NotificationTrigger.LocalTime"/> in the recipient's time zone.</summary>
    Daily = 1,

    /// <summary>On the chosen <see cref="NotificationTrigger.Days"/> at <see cref="NotificationTrigger.LocalTime"/> in the recipient's zone.</summary>
    Weekdays = 2,

    /// <summary>
    /// <see cref="NotificationTrigger.OffsetMinutes"/> from each occurrence of a named event, such as an
    /// eating window closing; the event's source says who it happens to and when.
    /// </summary>
    EventOffset = 3,

    /// <summary>
    /// Every <see cref="NotificationTrigger.IntervalMinutes"/> from <see cref="NotificationTrigger.At"/>, the first
    /// firing. A whole number of days keeps the recipient's wall-clock time (<see cref="NotificationTrigger.LocalTime"/>)
    /// across daylight saving; a shorter interval counts real minutes.
    /// </summary>
    Interval = 4,

    /// <summary>
    /// Once a month at <see cref="NotificationTrigger.LocalTime"/> on the recipient's clock: on day
    /// <see cref="NotificationTrigger.MonthDay"/> (a day past the month's end is its last day), or on the
    /// <see cref="NotificationTrigger.MonthWeek"/>th of the weekday in <see cref="NotificationTrigger.Days"/>.
    /// </summary>
    Monthly = 5,
}

/// <summary>When a schedule fires. Stored as JSON on the schedule; only the fields its kind uses are set.</summary>
public class NotificationTrigger
{
    public NotificationTriggerKind Kind { get; set; }

    /// <summary>Once: the instant, in UTC.</summary>
    public DateTime? At { get; set; }

    /// <summary>Daily and Weekdays: the time on the recipient's clock.</summary>
    public TimeOnly? LocalTime { get; set; }

    /// <summary>Weekdays: one bit per <see cref="DayOfWeek"/>, Sunday as bit 0.</summary>
    public int Days { get; set; }

    /// <summary>EventOffset: the event's name, for example <c>plan.eating_window_end</c>.</summary>
    public string? Event { get; set; }

    /// <summary>EventOffset: minutes after the event; negative is before it.</summary>
    public int OffsetMinutes { get; set; }

    /// <summary>Interval: minutes between firings.</summary>
    public int IntervalMinutes { get; set; }

    /// <summary>Monthly on a date: the day, 1 to 31. A month shorter than that fires on its last day.</summary>
    public int? MonthDay { get; set; }

    /// <summary>Monthly on a weekday: which one of the month, 1 to 4, or <see cref="LastWeek"/> for the last.</summary>
    public int? MonthWeek { get; set; }

    /// <summary>The <see cref="MonthWeek"/> that means the month's last such weekday.</summary>
    public const int LastWeek = 5;

    public static NotificationTrigger Once(DateTime atUtc) => new() { Kind = NotificationTriggerKind.Once, At = DateTime.SpecifyKind(atUtc, DateTimeKind.Utc) };

    public static NotificationTrigger Daily(TimeOnly localTime) => new() { Kind = NotificationTriggerKind.Daily, LocalTime = localTime };

    public static NotificationTrigger OnDays(TimeOnly localTime, params DayOfWeek[] days) =>
        new() { Kind = NotificationTriggerKind.Weekdays, LocalTime = localTime, Days = DaysMask(days) };

    public static NotificationTrigger AfterEvent(string name, int offsetMinutes) =>
        new() { Kind = NotificationTriggerKind.EventOffset, Event = name, OffsetMinutes = offsetMinutes };

    /// <summary>Every <paramref name="minutes"/> from <paramref name="firstUtc"/>; <paramref name="localTime"/> is the first firing's time on the recipient's clock.</summary>
    public static NotificationTrigger Every(DateTime firstUtc, int minutes, TimeOnly? localTime = null) =>
        new() { Kind = NotificationTriggerKind.Interval, At = DateTime.SpecifyKind(firstUtc, DateTimeKind.Utc), IntervalMinutes = minutes, LocalTime = localTime };

    /// <summary>Monthly on day <paramref name="day"/> (1 to 31) at <paramref name="localTime"/>.</summary>
    public static NotificationTrigger MonthlyOnDay(TimeOnly localTime, int day) =>
        new() { Kind = NotificationTriggerKind.Monthly, LocalTime = localTime, MonthDay = day };

    /// <summary>Monthly on the <paramref name="week"/>th <paramref name="day"/> (1 to 4, or <see cref="LastWeek"/>) at <paramref name="localTime"/>.</summary>
    public static NotificationTrigger MonthlyOnWeekday(TimeOnly localTime, int week, DayOfWeek day) =>
        new() { Kind = NotificationTriggerKind.Monthly, LocalTime = localTime, MonthWeek = week, Days = DaysMask([day]) };

    public static int DaysMask(IEnumerable<DayOfWeek> days) => days.Aggregate(0, (mask, day) => mask | (1 << (int)day));

    public bool Includes(DayOfWeek day) => (Days & (1 << (int)day)) != 0;
}

/// <summary>
/// When a timed trigger next fires (docs/ARCHITECTURE.md#notifications). A recurring time is the
/// recipient's wall clock: 07:00 in Addis Ababa is 04:00 UTC all year, and 07:00 in New York moves
/// with daylight saving. A time the clocks skip (02:30 on the spring-forward night) fires at the
/// same distance past the gap, 03:30; a time the clocks repeat fires the first time it happens.
/// </summary>
public static class NotificationOccurrences
{
    /// <summary>The first firing strictly after <paramref name="after"/>, or null when there is none (a past once, an event trigger, no days).</summary>
    public static DateTimeOffset? Next(NotificationTrigger trigger, TimeZoneInfo zone, DateTimeOffset after)
    {
        switch (trigger.Kind)
        {
            case NotificationTriggerKind.Once:
                if (trigger.At is not { } at) return null;
                var once = new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc));
                return once > after ? once : null;

            case NotificationTriggerKind.Daily:
            case NotificationTriggerKind.Weekdays:
                if (trigger.LocalTime is not { } time) return null;
                if (trigger.Kind == NotificationTriggerKind.Weekdays && (trigger.Days & 0x7F) == 0) return null;

                var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);
                // Start a day early: a zone's day can begin before the UTC instant's local date says.
                for (var day = localDay.AddDays(-1); day <= localDay.AddDays(8); day = day.AddDays(1))
                {
                    if (trigger.Kind == NotificationTriggerKind.Weekdays && !trigger.Includes(day.DayOfWeek)) continue;
                    var candidate = Instant(day, time, zone);
                    if (candidate > after) return candidate;
                }
                return null;

            case NotificationTriggerKind.Interval:
                if (trigger.At is not { } first || trigger.IntervalMinutes <= 0) return null;
                var start = new DateTimeOffset(DateTime.SpecifyKind(first, DateTimeKind.Utc));
                if (start > after) return start;

                if (trigger.IntervalMinutes % MinutesPerDay == 0)
                {
                    // Whole days step along the recipient's calendar, so 08:00 stays 08:00.
                    var everyDays = trigger.IntervalMinutes / MinutesPerDay;
                    var startLocal = TimeZoneInfo.ConvertTime(start, zone);
                    var startDay = DateOnly.FromDateTime(startLocal.DateTime);
                    var clock = trigger.LocalTime ?? TimeOnly.FromDateTime(startLocal.DateTime);
                    var afterDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);
                    var steps = Math.Max(0, (afterDay.DayNumber - startDay.DayNumber) / everyDays - 1);
                    for (var i = 0; i < 4; i++, steps++)
                    {
                        var candidate = Instant(startDay.AddDays(steps * everyDays), clock, zone);
                        if (candidate > after) return candidate;
                    }
                    return null;
                }

                var elapsed = (long)Math.Floor((after - start).TotalMinutes / trigger.IntervalMinutes) + 1;
                var next = start.AddMinutes(elapsed * trigger.IntervalMinutes);
                return next > after ? next : next.AddMinutes(trigger.IntervalMinutes);

            case NotificationTriggerKind.Monthly:
                {
                    if (trigger.LocalTime is not { } clockTime) return null;
                    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);
                    // A month early, for the same reason as above; fourteen months always holds the next one.
                    var month = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
                    for (var i = 0; i < 14; i++, month = month.AddMonths(1))
                    {
                        if (DayInMonth(trigger, month.Year, month.Month) is not { } day) return null;
                        var candidate = Instant(day, clockTime, zone);
                        if (candidate > after) return candidate;
                    }
                    return null;
                }

            default:
                return null;
        }
    }

    /// <summary>The day a monthly trigger falls on in a month, or null when the trigger names no day.</summary>
    public static DateOnly? DayInMonth(NotificationTrigger trigger, int year, int month)
    {
        var length = DateTime.DaysInMonth(year, month);
        if (trigger.MonthDay is { } date and >= 1 and <= 31)
            return new DateOnly(year, month, Math.Min(date, length));

        if (trigger.MonthWeek is not ({ } week and >= 1 and <= NotificationTrigger.LastWeek)) return null;
        var weekday = Enumerable.Range(0, 7).Select(d => (DayOfWeek)d).Where(trigger.Includes).Cast<DayOfWeek?>().FirstOrDefault();
        if (weekday is not { } wanted) return null;

        if (week == NotificationTrigger.LastWeek)
        {
            var last = new DateOnly(year, month, length);
            return last.AddDays(-(((int)last.DayOfWeek - (int)wanted + 7) % 7));
        }

        var first = new DateOnly(year, month, 1);
        return first.AddDays(((int)wanted - (int)first.DayOfWeek + 7) % 7 + 7 * (week - 1));
    }

    private const int MinutesPerDay = 24 * 60;

    /// <summary>The instant a wall-clock time on a local day happens in a zone, with the gap and overlap rules above.</summary>
    public static DateTimeOffset Instant(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            // Skipped by spring forward: move on by the length of the gap.
            var before = zone.GetUtcOffset(local.AddHours(-3));
            var afterGap = zone.GetUtcOffset(local.AddHours(3));
            local = local + (afterGap - before);
            return new DateTimeOffset(local, afterGap);
        }

        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            return new DateTimeOffset(local, offsets.Max());
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
