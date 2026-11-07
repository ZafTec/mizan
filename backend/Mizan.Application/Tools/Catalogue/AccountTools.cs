using System.Text.Json.Nodes;
using Mizan.Application.Ai;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Commands;
using Mizan.Application.Exceptions;
using Mizan.Application.Notifications;
using Mizan.Application.Queries;
using Mizan.Contracts.Mcp;
using Mizan.Contracts.Notifications;
using Mizan.Contracts.Users;
using Mizan.Domain.Ai;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>The person's account: profile, plan, AI allowance, achievements, notifications and reminders, and their settings.</summary>
public static class AccountTools
{
    private const string MarkAll = "all";

    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "get_my_account",
            Title = "My account",
            Description = "Read one part of the person's account: profile (name, targets, streak, price region), plan, AI usage and consent, "
                + "achievements and streaks, notifications, notification settings, or reminders.",
            Kind = ToolKind.Read,
            ModeArgument = "section",
            DefaultMode = "profile",
            Properties = new JsonObject
            {
                ["days"] = ToolSchema.Integer("ai_usage: days of history, 1 to 90 (14 by default)."),
                ["streakType"] = ToolSchema.Enum("achievements: the streak instead of the list.", ["nutrition", "workout"]),
                ["query"] = ToolSchema.String("achievements: part of a name, description or category."),
                ["category"] = ToolSchema.String("achievements: one category, such as nutrition or workout."),
                ["page"] = ToolSchema.Integer("achievements, notifications: page, from 1."),
                ["pageSize"] = ToolSchema.Integer("achievements, notifications: results per page, 20 by default."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "profile",
                    Summary = "Name, email, the active goal's targets, streak, price region, and whether notifications also go to Telegram. The default.",
                    Area = ToolMode.AnyConnection,
                    Requests = [typeof(GetUserQuery)],
                    Replaces = ["get_my_profile"],
                    Build = (_, context) => new GetUserQuery(context.UserId),
                },
                new ToolMode
                {
                    Name = "plan",
                    Summary = "The plan and its status. Free connections may make 15 successful tool calls a calendar month.",
                    Area = ToolMode.AnyConnection,
                    Requests = [typeof(GetMySubscriptionQuery)],
                    Replaces = ["get_my_subscription"],
                    Build = (_, _) => new GetMySubscriptionQuery(),
                },
                new ToolMode
                {
                    Name = "ai_usage",
                    Summary = "The person's own assistant usage over days, and what is left of today's allowance.",
                    Area = ToolMode.AnyConnection,
                    Requests = [typeof(GetMyAiUsageQuery)],
                    Replaces = ["get_ai_usage"],
                    Build = (args, _) => new GetMyAiUsageQuery(Math.Clamp(args.OptionalInt("days") ?? 14, 1, 90)),
                },
                new ToolMode
                {
                    Name = "ai_consent",
                    Summary = "What the in-app assistant may read and change, per area. Only the person changes it, in Settings.",
                    Area = ToolMode.AnyConnection,
                    Requests = [typeof(GetAiConsentQuery)],
                    Replaces = ["get_ai_consent"],
                    Build = (_, _) => new GetAiConsentQuery(),
                },
                new ToolMode
                {
                    Name = "achievements",
                    Summary = "Achievements earned and available; with streakType, that streak instead.",
                    Area = ToolMode.AnyConnection,
                    Requests = [typeof(GetAchievementsQuery), typeof(GetStreakQuery)],
                    Replaces = ["list_achievements", "get_streak"],
                    Build = (args, _) => args.OptionalString("streakType") is { Length: > 0 } streakType
                        ? new GetStreakQuery { StreakType = streakType }
                        : new GetAchievementsQuery
                        {
                            SearchTerm = args.OptionalString("query"),
                            Category = args.OptionalString("category"),
                            Page = Math.Max(1, args.OptionalInt("page") ?? 1),
                            PageSize = Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 200),
                        },
                },
                new ToolMode
                {
                    Name = "notifications",
                    Summary = "Notifications, newest first, with the unread count.",
                    Area = McpScopes.SocialRead,
                    Requests = [typeof(GetNotificationsQuery)],
                    Replaces = ["list_notifications", "get_unread_notification_count"],
                    Build = (args, _) => new GetNotificationsQuery(
                        Math.Max(1, args.OptionalInt("page") ?? 1),
                        Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 100)),
                },
                new ToolMode
                {
                    Name = "notification_settings",
                    Summary = "Which channels (inApp, telegram, push, email) each kind of notification uses, and the quiet hours.",
                    Area = McpScopes.SocialRead,
                    Requests = [typeof(GetNotificationPreferencesQuery)],
                    Replaces = ["get_notification_preferences"],
                    Build = (_, _) => new GetNotificationPreferencesQuery(),
                },
                new ToolMode
                {
                    Name = "reminders",
                    Summary = "The person's reminders, each with its schedule and when it next fires, and how many more the plan allows on.",
                    Area = McpScopes.PlanningRead,
                    Surfaces = ToolSurface.Both,
                    Axis = DataAxis.Planning,
                    Requests = [typeof(ListRemindersQuery)],
                    Replaces = ["list_reminders"],
                    Build = (_, _) => new ListRemindersQuery(),
                    Describe = result => result is ReminderListResult list ? AiReminderTools.Describe(list) : "No reminders.",
                },
            ],
        },
        new TaskTool
        {
            Name = "update_my_settings",
            Title = "Change my settings",
            Description = "Change the person's profile settings, which channels their notifications use and the quiet hours, or mark notifications read. "
                + "Change only what the person asked for: a field left out keeps its value.",
            Kind = ToolKind.Write,
            ModeArgument = "section",
            DefaultMode = "profile",
            Properties = new JsonObject
            {
                ["profile"] = ToolSchema.Object(
                    "profile: the fields to change.",
                    new JsonObject
                    {
                        ["name"] = ToolSchema.String("Display name."),
                        ["image"] = ToolSchema.String("Avatar address."),
                        ["themePreference"] = ToolSchema.Enum("Theme.", ["light", "dark", "system"]),
                        ["compactMode"] = ToolSchema.Boolean("Denser layout."),
                        ["reduceAnimations"] = ToolSchema.Boolean("Less motion."),
                        ["priceRegionCode"] = ToolSchema.String("Price region, from mizan://reference/price-regions."),
                        ["notifyByTelegram"] = ToolSchema.Boolean("Also send notifications to the linked Telegram chat; refused until a chat is linked."),
                    }),
                ["notifications"] = ToolSchema.Object(
                    "notifications: kinds to change and the quiet hours.",
                    new JsonObject
                    {
                        ["kinds"] = ToolSchema.Array(
                            "Kinds to change: achievements, reminders, planning, coaching, household, social, account, marketing. A channel left out keeps its value.",
                            ToolSchema.Object("One kind's channels.", new JsonObject
                            {
                                ["kind"] = ToolSchema.String("The kind."),
                                ["inApp"] = ToolSchema.Boolean("In the app."),
                                ["telegram"] = ToolSchema.Boolean("Telegram."),
                                ["push"] = ToolSchema.Boolean("Push."),
                                ["email"] = ToolSchema.Boolean("Email."),
                            }, "kind")),
                        ["quietHoursStart"] = ToolSchema.String("Start of quiet hours, HH:mm; set with quietHoursEnd."),
                        ["quietHoursEnd"] = ToolSchema.String("End of quiet hours, HH:mm."),
                        ["clearQuietHours"] = ToolSchema.Boolean("True removes the quiet hours."),
                    }),
                ["markRead"] = ToolSchema.Array("mark_read: the notification ids, or [\"all\"] for every one.", new JsonObject { ["type"] = "string" }),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "profile",
                    Summary = "Change the profile fields given in profile. The default.",
                    Area = ToolMode.AnyWrite,
                    Requests = [typeof(UpdateUserCommand)],
                    Replaces = ["update_my_profile"],
                    Build = (args, context) =>
                    {
                        var profile = args.As<UpdateUserRequest>("profile")
                            ?? throw new DomainValidationException("'profile' is required: the fields to change.");
                        return new UpdateUserCommand(
                            context.UserId,
                            profile.Name,
                            profile.Image,
                            profile.ThemePreference,
                            profile.CompactMode,
                            profile.ReduceAnimations,
                            PriceRegionCode: profile.PriceRegionCode,
                            NotifyByTelegram: profile.NotifyByTelegram);
                    },
                    Shape = result => new { updated = result is true },
                },
                new ToolMode
                {
                    Name = "notifications",
                    Summary = "Change notification channels or quiet hours, as given in notifications; returns every preference.",
                    Area = McpScopes.SocialWrite,
                    Requests = [typeof(UpdateNotificationPreferencesCommand)],
                    Replaces = ["update_notification_preferences"],
                    Build = (args, _) =>
                    {
                        var change = args.As<UpdateNotificationPreferencesRequest>("notifications")
                            ?? throw new DomainValidationException("'notifications' is required: the kinds or quiet hours to change.");
                        return new UpdateNotificationPreferencesCommand(change.Kinds, change.QuietHoursStart, change.QuietHoursEnd, change.ClearQuietHours);
                    },
                },
                new ToolMode
                {
                    Name = "mark_read",
                    Summary = "Mark the notifications in markRead read, or all of them with [\"all\"].",
                    Area = McpScopes.SocialWrite,
                    Requests = [typeof(MarkAllNotificationsReadCommand), typeof(MarkNotificationsReadCommand)],
                    Replaces = ["mark_notification_read", "mark_all_notifications_read"],
                    Build = (args, _) =>
                    {
                        var marks = args.StringList("markRead");
                        if (marks.Count == 1 && string.Equals(marks[0], MarkAll, StringComparison.OrdinalIgnoreCase))
                            return new MarkAllNotificationsReadCommand();
                        return new MarkNotificationsReadCommand(args.GuidList("markRead"));
                    },
                    Shape = result => result is int marked ? new { marked } : (object)new { all = true },
                },
            ],
        },
    ];

    public static IReadOnlyList<ToolMode> DeleteKinds { get; } = [];
}
