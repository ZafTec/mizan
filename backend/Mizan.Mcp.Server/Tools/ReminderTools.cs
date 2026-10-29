using System.ComponentModel;
using Mizan.Contracts.Notifications;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

/// <summary>The person's reminders (docs/ARCHITECTURE.md#reminders). Each tool is one call to /api/Reminders; the API holds every rule.</summary>
[McpServerToolType]
public sealed class ReminderTools
{
    private const string ScheduleHelp =
        "When it fires, on the person's clock: { repeat: once|daily|days|every, time: HH:mm, days: [mon..sun] for days, "
        + "date: yyyy-MM-dd for once or the first day of every, everyHours | everyDays | everyMinutes (at least 60) for every }.";

    private readonly IBackendApiClient _api;
    public ReminderTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_reminders", ReadOnly = true, Idempotent = true)]
    [Description("List the person's reminders, with a summary of each schedule, when it next fires, and how many more their plan allows on.")]
    public Task<string> List(CancellationToken ct = default) => _api.GetAsync("/api/Reminders", ct);

    [McpServerTool(Name = "create_reminder")]
    [Description("Set a reminder. Kinds: workout, measurement, pre_cook, meal_log, custom. A custom reminder needs a title. A pre_cook reminder takes its day from a meal plan's prep session (mealPlanId, prepSessionId) and needs no schedule. Reminders repeat at most once an hour, quiet hours still apply, and the plan caps how many are on.")]
    public Task<string> Create(
        [Description("workout, measurement, pre_cook, meal_log or custom.")] string kind,
        [Description(ScheduleHelp)] ReminderSchedule? schedule = null,
        [Description("What the reminder says. Each kind but custom has a default.")] string? title = null,
        [Description("A line under the title.")] string? note = null,
        [Description("inApp, telegram, push, email. All by default; the person's preferences for reminders still apply.")] string[]? channels = null,
        [Description("Workout: the template to train.")] string? workoutTemplateId = null,
        [Description("Pre-cook: the meal plan.")] string? mealPlanId = null,
        [Description("Pre-cook: the plan's prep session.")] string? prepSessionId = null,
        [Description("Pre-cook: when the prep starts, HH:mm. 18:00 by default.")] string? prepTime = null,
        [Description("Pre-cook: minutes before the prep. 60 by default.")] int? leadMinutes = null,
        [Description("Meal log: BREAKFAST, LUNCH, DINNER or SNACK.")] string? mealType = null,
        CancellationToken ct = default) =>
        _api.PostAsync("/api/Reminders", new CreateReminderRequest(
            kind, title, note, schedule, channels,
            ToolArguments.ParseOptionalId(workoutTemplateId, nameof(workoutTemplateId)),
            ToolArguments.ParseOptionalId(mealPlanId, nameof(mealPlanId)),
            ToolArguments.ParseOptionalId(prepSessionId, nameof(prepSessionId)),
            prepTime, leadMinutes, mealType), ct);

    [McpServerTool(Name = "update_reminder", Idempotent = true)]
    [Description("Change a reminder: its title, note, schedule, channels, or turn it off and on (enabled). A field left out keeps its value. A pre_cook reminder changes through prepTime and leadMinutes, not a schedule.")]
    public Task<string> Update(
        string id,
        [Description(ScheduleHelp)] ReminderSchedule? schedule = null,
        string? title = null,
        string? note = null,
        string[]? channels = null,
        bool? enabled = null,
        string? prepTime = null,
        int? leadMinutes = null,
        CancellationToken ct = default) =>
        _api.PutAsync($"/api/Reminders/{ToolArguments.ParseId(id, nameof(id))}", new UpdateReminderRequest(title, note, schedule, channels, enabled, prepTime, leadMinutes), ct);

    [McpServerTool(Name = "delete_reminder", Destructive = true, Idempotent = true)]
    [Description("Delete a reminder. Confirm with the person first; turning it off (update_reminder with enabled false) keeps it.")]
    public Task<string> Delete(string id, CancellationToken ct = default) =>
        _api.DeleteAsync($"/api/Reminders/{ToolArguments.ParseId(id, nameof(id))}", ct);
}
