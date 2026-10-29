using System.Globalization;

namespace Mizan.Domain.Notifications;

/// <summary>
/// What a reminder is about (docs/ARCHITECTURE.md#reminders). The kind gives the reminder its
/// default wording, the page its message opens, and the part of the log an assistant needs
/// permission to write before it may set one.
/// </summary>
public static class ReminderKinds
{
    public const string Workout = "workout";
    public const string Measurement = "measurement";
    public const string PreCook = "pre_cook";
    public const string MealLog = "meal_log";
    public const string Custom = "custom";

    public static IReadOnlyList<string> All { get; } = [Workout, Measurement, PreCook, MealLog, Custom];

    public static bool IsKnown(string? kind) => kind is not null && All.Contains(kind);

    public static string Label(string kind) => kind switch
    {
        Workout => "Workout",
        Measurement => "Measurement",
        PreCook => "Prep",
        MealLog => "Meal log",
        _ => "Reminder",
    };

    /// <summary>The title a reminder gets when the person gives none.</summary>
    public static string DefaultTitle(string kind, string? subject = null) => kind switch
    {
        Workout => subject is { Length: > 0 } template ? $"Workout: {template}" : "Time for your workout",
        Measurement => "Time to take your measurements",
        PreCook => subject is { Length: > 0 } session ? $"Start the prep: {session}" : "Time to start the prep",
        MealLog => subject is { Length: > 0 } meal ? $"Log your {meal.ToLowerInvariant()}" : "Log your meal",
        _ => "Reminder",
    };

    /// <summary>The button a reminder's message carries, and where it goes.</summary>
    public static (string Label, string Path) Action(string kind, Guid? mealPlanId = null, DateOnly? planDay = null) => kind switch
    {
        Workout => ("Start a workout", "/workouts"),
        Measurement => ("Log measurements", "/body-measurements"),
        PreCook when mealPlanId is { } plan => ("Open the plan", planDay is { } day ? $"/meal-plan/{plan:D}?day={day:yyyy-MM-dd}" : $"/meal-plan/{plan:D}"),
        MealLog => ("Log a meal", "/meals/add"),
        _ => ("Open Mizan", "/today"),
    };
}

/// <summary>How a person asked a reminder to repeat, before it becomes a trigger. Strings, as every surface sends them.</summary>
/// <param name="Repeat"><c>once</c>, <c>daily</c>, <c>days</c> (chosen weekdays) or <c>every</c> (an interval).</param>
/// <param name="Time"><c>HH:mm</c> on the person's clock.</param>
/// <param name="Days"><c>mon</c> to <c>sun</c>, for <c>days</c>.</param>
/// <param name="Date"><c>yyyy-MM-dd</c>: the day of a <c>once</c>, or the first day of an <c>every</c>.</param>
public sealed record ReminderTiming(
    string? Repeat,
    string? Time = null,
    IReadOnlyList<string>? Days = null,
    string? Date = null,
    int? EveryMinutes = null,
    int? EveryHours = null,
    int? EveryDays = null);

/// <summary>A trigger, or why the request cannot be one.</summary>
public sealed record ReminderTriggerResult(NotificationTrigger? Trigger, string? Error)
{
    public static ReminderTriggerResult Fail(string error) => new(null, error);
}

/// <summary>
/// The rules every reminder follows, whichever surface set it (docs/ARCHITECTURE.md#reminders): the
/// person's own clock, nothing more often than once an hour, a cap on active reminders that depends
/// on the plan, and the wording of a schedule. Application turns an error string into a 400.
/// </summary>
public static class ReminderRules
{
    public const int FreeActiveLimit = 3;
    public const int ProActiveLimit = 30;
    public const int MinIntervalMinutes = 60;
    public const int MaxIntervalDays = 365;
    public const int MaxTitleLength = 120;
    public const int MaxNoteLength = 500;
    public const int DefaultSnoozeMinutes = 30;
    public const int MinSnoozeMinutes = 5;
    public const int MaxSnoozeMinutes = 12 * 60;
    public const int DefaultLeadMinutes = 60;
    public const int MaxLeadMinutes = 24 * 60;

    public static readonly TimeOnly DefaultPrepTime = new(18, 0);

    public static int ActiveLimit(bool isPro) => isPro ? ProActiveLimit : FreeActiveLimit;

    public const string TooOften = "A reminder can repeat at most once an hour.";

    private static readonly string[] DayNames = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    public static string DayName(DayOfWeek day) => DayNames[(int)day];

    public static bool TryParseDay(string? value, out DayOfWeek day)
    {
        var key = value?.Trim().ToLowerInvariant();
        var index = key is { Length: >= 3 } ? Array.IndexOf(DayNames, key[..3]) : -1;
        day = index < 0 ? default : (DayOfWeek)index;
        return index >= 0;
    }

    public static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value?.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The trigger for a timing, in the person's zone, or the reason it is refused.</summary>
    public static ReminderTriggerResult Trigger(ReminderTiming timing, TimeZoneInfo zone, DateTimeOffset now)
    {
        var repeat = timing.Repeat?.Trim().ToLowerInvariant();
        TimeOnly? time = null;
        if (!string.IsNullOrWhiteSpace(timing.Time))
        {
            if (!TryParseTime(timing.Time, out var parsed)) return ReminderTriggerResult.Fail("Give the time as HH:mm, such as 07:00.");
            time = parsed;
        }

        DateOnly? date = null;
        if (!string.IsNullOrWhiteSpace(timing.Date))
        {
            if (!DateOnly.TryParseExact(timing.Date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return ReminderTriggerResult.Fail("Give the date as yyyy-MM-dd.");
            date = parsed;
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        switch (repeat)
        {
            case "once":
                {
                    if (time is not { } at) return ReminderTriggerResult.Fail("A one-off reminder needs a time.");
                    var instant = NotificationOccurrences.Instant(date ?? today, at, zone);
                    if (instant <= now) return ReminderTriggerResult.Fail("That time has already passed.");
                    return new(NotificationTrigger.Once(instant.UtcDateTime), null);
                }

            case "daily":
                if (time is not { } daily) return ReminderTriggerResult.Fail("A daily reminder needs a time.");
                return new(NotificationTrigger.Daily(daily), null);

            case "days":
            case "weekdays":
                {
                    if (time is not { } at) return ReminderTriggerResult.Fail("A reminder on chosen days needs a time.");
                    var days = new List<DayOfWeek>();
                    foreach (var name in timing.Days ?? [])
                    {
                        if (!TryParseDay(name, out var day)) return ReminderTriggerResult.Fail($"\"{name}\" is not a day. Use mon, tue, wed, thu, fri, sat or sun.");
                        days.Add(day);
                    }
                    if (days.Count == 0) return ReminderTriggerResult.Fail("Choose at least one day.");
                    return new(NotificationTrigger.OnDays(at, [.. days.Distinct()]), null);
                }

            case "every":
                {
                    var given = new[] { timing.EveryMinutes, timing.EveryHours, timing.EveryDays }.Count(v => v is not null);
                    if (given != 1) return ReminderTriggerResult.Fail("Say how often it repeats, such as every 3 hours or every 2 days.");
                    var minutes = timing.EveryMinutes
                        ?? (timing.EveryHours is { } hours ? (long)hours * 60 : (long)timing.EveryDays!.Value * 24 * 60);
                    if (minutes < MinIntervalMinutes) return ReminderTriggerResult.Fail(TooOften);
                    if (minutes > MaxIntervalDays * 24L * 60) return ReminderTriggerResult.Fail($"A reminder repeats at least once every {MaxIntervalDays} days.");

                    DateTimeOffset first;
                    if (minutes % (24 * 60) == 0)
                    {
                        if (time is not { } at) return ReminderTriggerResult.Fail("A reminder every few days needs a time.");
                        first = NotificationOccurrences.Instant(date ?? today, at, zone);
                    }
                    else
                    {
                        first = time is { } at
                            ? NotificationOccurrences.Instant(date ?? today, at, zone)
                            : TruncateToMinute(now).AddMinutes(minutes);
                    }

                    var trigger = NotificationTrigger.Every(first.UtcDateTime, (int)minutes, time);
                    // The first firing may be past already (07:00 when it is noon): start from the next one.
                    if (first <= now && NotificationOccurrences.Next(trigger, zone, now) is { } next)
                        trigger = NotificationTrigger.Every(next.UtcDateTime, (int)minutes, time);
                    return new(trigger, null);
                }

            default:
                return ReminderTriggerResult.Fail("Repeat is once, daily, days or every.");
        }
    }

    /// <summary>When a pre-cook reminder fires: <paramref name="leadMinutes"/> before the prep starts on its plan day.</summary>
    public static ReminderTriggerResult PreCook(DateOnly planDay, TimeOnly prepTime, int leadMinutes, TimeZoneInfo zone, DateTimeOffset now)
    {
        if (leadMinutes is < 0 or > MaxLeadMinutes) return ReminderTriggerResult.Fail($"Remind between 0 and {MaxLeadMinutes / 60} hours before the prep.");
        var at = NotificationOccurrences.Instant(planDay, prepTime, zone).AddMinutes(-leadMinutes);
        if (at <= now) return ReminderTriggerResult.Fail("That prep session is too soon, or already past, to remind you about.");
        return new(NotificationTrigger.Once(at.UtcDateTime), null);
    }

    /// <summary>How many minutes a snooze lasts, or why it cannot.</summary>
    public static string? CheckSnooze(int minutes) =>
        minutes is < MinSnoozeMinutes or > MaxSnoozeMinutes
            ? $"Snooze for between {MinSnoozeMinutes} minutes and {MaxSnoozeMinutes / 60} hours."
            : null;

    /// <summary>The start of the person's next day: "skip today" holds every delivery before it.</summary>
    public static DateTimeOffset EndOfToday(DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return NotificationOccurrences.Instant(today.AddDays(1), TimeOnly.MinValue, zone);
    }

    /// <summary>The schedule in words, the same on every surface: "Weekdays at 07:00", "Every 3 days at 08:00".</summary>
    public static string Describe(NotificationTrigger trigger, TimeZoneInfo zone)
    {
        var at = trigger.LocalTime is { } time ? $" at {FormatTime(time)}" : string.Empty;
        switch (trigger.Kind)
        {
            case NotificationTriggerKind.Once:
                if (trigger.At is not { } once) return "Once";
                var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(once, DateTimeKind.Utc)), zone);
                return $"Once, {local.ToString("ddd d MMM", CultureInfo.InvariantCulture)} at {local.ToString("HH:mm", CultureInfo.InvariantCulture)}";
            case NotificationTriggerKind.Daily:
                return $"Every day{at}";
            case NotificationTriggerKind.Weekdays:
                return $"{DescribeDays(trigger.Days)}{at}";
            case NotificationTriggerKind.Interval:
                var minutes = trigger.IntervalMinutes;
                if (minutes % (24 * 60) == 0)
                {
                    var days = minutes / (24 * 60);
                    return (days == 1 ? "Every day" : days == 7 ? "Every week" : $"Every {days} days") + at;
                }
                if (minutes % 60 == 0) return minutes == 60 ? "Every hour" : $"Every {minutes / 60} hours";
                return $"Every {minutes} minutes";
            default:
                return "When the event happens";
        }
    }

    /// <summary>The trigger's days as the names every surface sends back: <c>mon</c> to <c>sun</c>, Monday first.</summary>
    public static IReadOnlyList<string> DayNamesOf(NotificationTrigger trigger) =>
        trigger.Kind == NotificationTriggerKind.Weekdays
            ? MondayFirst.Where(trigger.Includes).Select(DayName).ToList()
            : [];

    /// <summary>The trigger's repeat as the request spells it.</summary>
    public static string RepeatOf(NotificationTrigger trigger) => trigger.Kind switch
    {
        NotificationTriggerKind.Once => "once",
        NotificationTriggerKind.Daily => "daily",
        NotificationTriggerKind.Weekdays => "days",
        NotificationTriggerKind.Interval => "every",
        _ => "event",
    };

    private static readonly DayOfWeek[] MondayFirst =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    private static string DescribeDays(int mask)
    {
        const int weekdays = 0b0111110, weekend = 0b1000001, all = 0b1111111;
        return (mask & all) switch
        {
            all => "Every day",
            weekdays => "Weekdays",
            weekend => "Weekends",
            var m => string.Join(", ", MondayFirst.Where(d => (m & (1 << (int)d)) != 0)
                .Select(d => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(d))),
        };
    }

    private static DateTimeOffset TruncateToMinute(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Offset);
}
