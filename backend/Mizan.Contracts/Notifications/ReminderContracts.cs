namespace Mizan.Contracts.Notifications;

/// <summary>
/// How a reminder repeats, on the person's clock (docs/ARCHITECTURE.md#reminders).
/// </summary>
/// <param name="Repeat"><c>once</c>, <c>daily</c>, <c>days</c> (the chosen <see cref="Days"/>) or <c>every</c> (an interval).</param>
/// <param name="Time"><c>HH:mm</c>, such as <c>07:00</c>. Every repeat needs it except an interval in hours.</param>
/// <param name="Days"><c>mon</c> to <c>sun</c>, for <c>days</c>.</param>
/// <param name="Date"><c>yyyy-MM-dd</c>: the day of a <c>once</c>, or the first day of an <c>every</c>. Defaults to today.</param>
/// <param name="EveryMinutes">For <c>every</c>, one of the three: at least 60.</param>
public record ReminderSchedule(
    string Repeat,
    string? Time = null,
    IReadOnlyList<string>? Days = null,
    string? Date = null,
    int? EveryMinutes = null,
    int? EveryHours = null,
    int? EveryDays = null);

/// <summary>
/// Body of <c>POST /api/Reminders</c>. A pre-cook reminder takes its day from the plan's prep session
/// and needs no <see cref="Schedule"/>; every other kind needs one.
/// </summary>
/// <param name="Kind"><c>workout</c>, <c>measurement</c>, <c>pre_cook</c>, <c>meal_log</c> or <c>custom</c>.</param>
/// <param name="Title">Required for <c>custom</c>; the others have a default.</param>
/// <param name="Channels"><c>inApp</c>, <c>telegram</c>, <c>push</c>, <c>email</c>; all by default. The person's choices for reminders still apply.</param>
/// <param name="PrepTime">Pre-cook: when the prep starts, <c>HH:mm</c>; 18:00 by default.</param>
/// <param name="LeadMinutes">Pre-cook: how long before the prep to remind; 60 by default.</param>
/// <param name="MealType">Meal log: <c>BREAKFAST</c>, <c>LUNCH</c>, <c>DINNER</c> or <c>SNACK</c>.</param>
public record CreateReminderRequest(
    string Kind,
    string? Title = null,
    string? Note = null,
    ReminderSchedule? Schedule = null,
    IReadOnlyList<string>? Channels = null,
    Guid? WorkoutTemplateId = null,
    Guid? MealPlanId = null,
    Guid? PrepSessionId = null,
    string? PrepTime = null,
    int? LeadMinutes = null,
    string? MealType = null);

/// <summary>Body of <c>PUT /api/Reminders/{id}</c>. A field left out keeps its value.</summary>
public record UpdateReminderRequest(
    string? Title = null,
    string? Note = null,
    ReminderSchedule? Schedule = null,
    IReadOnlyList<string>? Channels = null,
    bool? Enabled = null,
    string? PrepTime = null,
    int? LeadMinutes = null);

/// <summary>Body of <c>POST /api/Reminders/{id}/snooze</c>: 30 minutes when left out.</summary>
public record SnoozeReminderRequest(int? Minutes = null);

/// <summary>
/// One reminder as every surface shows it. <see cref="Summary"/> and <see cref="NextRunText"/> are
/// worded by the API on the person's clock; <see cref="Active"/> is whether it counts towards the cap
/// (on, and with a firing still to come).
/// </summary>
public record ReminderDto(
    Guid Id,
    string Kind,
    string KindLabel,
    string Title,
    string? Note,
    string Repeat,
    string? Time,
    IReadOnlyList<string> Days,
    string? Date,
    int? EveryMinutes,
    string Summary,
    IReadOnlyList<string> Channels,
    bool Enabled,
    bool Active,
    DateTime? NextRunAt,
    string? NextRunText,
    DateTime? SkipUntil,
    Guid? WorkoutTemplateId,
    Guid? MealPlanId,
    Guid? PrepSessionId,
    string? PrepTime,
    int? LeadMinutes,
    string? MealType,
    string ActionLabel,
    string ActionPath,
    DateTime CreatedAt);

/// <summary>What <c>GET /api/Reminders</c> answers: the person's reminders and how many more their plan allows.</summary>
public record ReminderListResult(IReadOnlyList<ReminderDto> Items, int ActiveCount, int ActiveLimit, bool IsPro);

/// <summary>
/// The answer to Done, Snooze and Skip today: the reminder as it now stands and one sentence saying
/// what happened, which the bot and the web show as they are.
/// </summary>
public record ReminderActionResult(ReminderDto Reminder, string Message, DateTime? SnoozedUntil = null);
