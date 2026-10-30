using System.Text;
using System.Text.Json;
using Mizan.Application.Notifications;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Ai;

namespace Mizan.Application.Ai.Tools;

/// <summary>
/// How the reminder task tools read a reminder from arguments and describe a list of them
/// (docs/ARCHITECTURE.md#reminders). The commands they build hold every rule (kinds, the hourly
/// floor, the plan's cap, quiet hours), the same for the web, MCP, the bot and the assistant.
/// </summary>
public static class AiReminderTools
{
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
            AiToolArgs.OptionalInt(args, "everyDays"),
            AiToolArgs.OptionalInt(args, "dayOfMonth"),
            AiToolArgs.OptionalString(args, "week"));
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

    public static string Describe(ReminderListResult list)
    {
        if (list.Items.Count == 0) return "The user has no reminders.";
        var text = new StringBuilder($"{list.ActiveCount} of {list.ActiveLimit} reminders on.\n");
        foreach (var r in list.Items)
            text.Append($"{r.Id} — {r.Title}: {r.Summary}{(r.Enabled ? r.NextRunText is { } next ? $", next {next}" : "" : ", off")}\n");
        return text.ToString().TrimEnd();
    }
}
