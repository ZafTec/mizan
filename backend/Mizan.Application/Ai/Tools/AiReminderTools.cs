using System.Text;
using System.Text.Json;
using Mizan.Application.Notifications;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Ai;

namespace Mizan.Application.Ai.Tools;

/// <summary>
/// The assistant's reminder tools (docs/ARCHITECTURE.md#reminders). They build the same commands the
/// web, MCP and the bot send, so every rule (kinds, the hourly floor, the plan's cap, quiet hours)
/// holds without the model knowing it. A reminder is something the person plans, so it sits on
/// <see cref="DataAxis.Planning"/>: creating one needs the nutrition write consent. Changing or
/// deleting one stays with the person (rule 3 of <see cref="AiToolCatalogue"/>).
/// </summary>
public static class AiReminderTools
{
    public static readonly IReadOnlyList<AiToolDefinition> All =
    [
        new AiToolDefinition(
            "create_reminder",
            "Set a reminder for the user. Times are on the user's own clock. A pre_cook reminder takes its day from a "
            + "meal plan's prep session and needs mealPlanId and prepSessionId instead of a repeat. A custom reminder needs a title.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "kind": { "type": "string", "enum": ["workout", "measurement", "pre_cook", "meal_log", "custom"] },
                "title": { "type": "string" },
                "note": { "type": "string" },
                "repeat": { "type": "string", "enum": ["once", "daily", "days", "every"] },
                "time": { "type": "string", "description": "HH:mm, e.g. 07:00" },
                "days": { "type": "array", "items": { "type": "string", "enum": ["mon", "tue", "wed", "thu", "fri", "sat", "sun"] } },
                "date": { "type": "string", "description": "YYYY-MM-DD: the day of a once, or the first day of an every" },
                "everyHours": { "type": "integer", "minimum": 1 },
                "everyDays": { "type": "integer", "minimum": 1 },
                "mealType": { "type": "string", "enum": ["BREAKFAST", "LUNCH", "DINNER", "SNACK"] },
                "workoutTemplateId": { "type": "string" },
                "mealPlanId": { "type": "string" },
                "prepSessionId": { "type": "string" },
                "prepTime": { "type": "string", "description": "HH:mm the prep starts, default 18:00" },
                "leadMinutes": { "type": "integer", "description": "How long before the prep to remind, default 60" }
              },
              "required": ["kind"]
            }
            """,
            (args, _) => new CreateReminderCommand(Request(args)),
            result => result is ReminderDto reminder
                ? $"Set the reminder \"{reminder.Title}\": {reminder.Summary}." + (reminder.NextRunText is { } next ? $" First one {next}." : "")
                : "Set that reminder.",
            DataAxis.Planning, AiToolAccess.Write),

        new AiToolDefinition(
            "list_reminders",
            "List the user's reminders with when each next fires.",
            """{ "type": "object", "additionalProperties": false, "properties": {}, "required": [] }""",
            (_, _) => new ListRemindersQuery(),
            result => result is ReminderListResult list ? Describe(list) : "No reminders.",
            DataAxis.Planning, AiToolAccess.Read),
    ];

    public static CreateReminderRequest Request(JsonElement args)
    {
        var kind = AiToolArgs.RequiredString(args, "kind");
        var repeat = AiToolArgs.OptionalString(args, "repeat");
        var schedule = repeat is null ? null : new ReminderSchedule(
            repeat,
            AiToolArgs.OptionalString(args, "time"),
            Strings(args, "days"),
            AiToolArgs.OptionalString(args, "date"),
            null,
            AiToolArgs.OptionalInt(args, "everyHours"),
            AiToolArgs.OptionalInt(args, "everyDays"));
        return new CreateReminderRequest(
            kind,
            AiToolArgs.OptionalString(args, "title"),
            AiToolArgs.OptionalString(args, "note"),
            schedule,
            null,
            AiToolArgs.OptionalGuid(args, "workoutTemplateId"),
            AiToolArgs.OptionalGuid(args, "mealPlanId"),
            AiToolArgs.OptionalGuid(args, "prepSessionId"),
            AiToolArgs.OptionalString(args, "prepTime"),
            AiToolArgs.OptionalInt(args, "leadMinutes"),
            AiToolArgs.OptionalString(args, "mealType"));
    }

    private static List<string>? Strings(JsonElement args, string field) =>
        args.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).ToList()
            : null;

    private static string Describe(ReminderListResult list)
    {
        if (list.Items.Count == 0) return "The user has no reminders.";
        var text = new StringBuilder($"{list.ActiveCount} of {list.ActiveLimit} reminders on.\n");
        foreach (var r in list.Items)
            text.Append($"{r.Id} — {r.Title}: {r.Summary}{(r.Enabled ? r.NextRunText is { } next ? $", next {next}" : "" : ", off")}\n");
        return text.ToString().TrimEnd();
    }
}
