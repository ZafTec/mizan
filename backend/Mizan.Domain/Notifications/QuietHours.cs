namespace Mizan.Domain.Notifications;

/// <summary>
/// A person's quiet hours, on their own clock. A notice that would reach Telegram, a phone or an
/// inbox during them waits until they end; the in-app list is silent and is never held back.
/// A range that crosses midnight (22:00 to 07:00) is the common case. Equal ends mean no quiet hours.
/// </summary>
public static class QuietHours
{
    /// <summary>The channels that make a sound and so wait out quiet hours: Telegram, push and email.</summary>
    public const NotificationChannels Held = NotificationChannels.Telegram | NotificationChannels.Push | NotificationChannels.Email;

    public static bool IsQuiet(TimeOnly start, TimeOnly end, TimeOnly now) =>
        start != end && (start < end ? now >= start && now < end : now >= start || now < end);

    /// <summary>When a notice raised at <paramref name="now"/> may go out: now, or the end of the quiet hours it falls in.</summary>
    public static DateTimeOffset Release(DateTimeOffset now, TimeZoneInfo zone, TimeOnly? start, TimeOnly? end)
    {
        if (start is not { } from || end is not { } to || from == to) return now;

        var local = TimeZoneInfo.ConvertTime(now, zone);
        var clock = TimeOnly.FromDateTime(local.DateTime);
        if (!IsQuiet(from, to, clock)) return now;

        var today = DateOnly.FromDateTime(local.DateTime);
        var endDay = clock >= to ? today.AddDays(1) : today;
        return NotificationOccurrences.Instant(endDay, to, zone);
    }

    /// <summary>When a notice on <paramref name="channel"/> may go out: the in-app list at once, the held channels at <see cref="Release"/>.</summary>
    public static DateTimeOffset ReleaseFor(NotificationChannels channel, DateTimeOffset now, TimeZoneInfo zone, TimeOnly? start, TimeOnly? end) =>
        (Held & channel) == channel && channel != NotificationChannels.None ? Release(now, zone, start, end) : now;
}
