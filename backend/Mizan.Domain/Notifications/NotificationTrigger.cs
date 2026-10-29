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

    public static NotificationTrigger Once(DateTime atUtc) => new() { Kind = NotificationTriggerKind.Once, At = DateTime.SpecifyKind(atUtc, DateTimeKind.Utc) };

    public static NotificationTrigger Daily(TimeOnly localTime) => new() { Kind = NotificationTriggerKind.Daily, LocalTime = localTime };

    public static NotificationTrigger OnDays(TimeOnly localTime, params DayOfWeek[] days) =>
        new() { Kind = NotificationTriggerKind.Weekdays, LocalTime = localTime, Days = DaysMask(days) };

    public static NotificationTrigger AfterEvent(string name, int offsetMinutes) =>
        new() { Kind = NotificationTriggerKind.EventOffset, Event = name, OffsetMinutes = offsetMinutes };

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

            default:
                return null;
        }
    }

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
