namespace Mizan.Domain.Notifications;

/// <summary>The channels a notification can reach. A set, so a schedule or a person's choice can name several.</summary>
[Flags]
public enum NotificationChannels
{
    None = 0,
    InApp = 1,
    Telegram = 2,
    Push = 4,
    Email = 8,
    All = InApp | Telegram | Push | Email,
}

/// <summary>One kind a person sets channels for, with the wording every surface shows and the channels it starts with.</summary>
public sealed record NotificationKindInfo(string Kind, string Label, string Description, NotificationChannels Defaults);

/// <summary>
/// What a notification is about, for the person's channel choices (docs/ARCHITECTURE.md#notifications).
/// A notification's <c>type</c> is specific (<c>achievement_unlocked</c>); its kind is the group a
/// person turns on or off. Marketing starts off on every channel and stays off until the person
/// turns it on; every other kind starts on in the app, Telegram and push, and off by email.
/// </summary>
public static class NotificationKinds
{
    public const string Achievements = "achievements";
    public const string Reminders = "reminders";
    public const string Planning = "planning";
    public const string Coaching = "coaching";
    public const string Household = "household";
    public const string Social = "social";
    public const string Account = "account";
    public const string Marketing = "marketing";

    private const NotificationChannels Product = NotificationChannels.InApp | NotificationChannels.Telegram | NotificationChannels.Push;

    public static IReadOnlyList<NotificationKindInfo> All { get; } =
    [
        new(Achievements, "Achievements and streaks", "Badges you earn and streak milestones.", Product),
        new(Reminders, "Reminders", "Reminders you set, and the evening reminder before your eating window closes.", Product),
        new(Planning, "Meal plans", "Plans drafted for you, and answers to plans you sent.", Product),
        new(Coaching, "Coaching", "Requests and changes from coaches and clients.", Product),
        new(Household, "Household", "Invitations and answers from your household.", Product),
        new(Social, "Social", "Follows, reactions and comments.", Product),
        new(Account, "Account and prices", "Price reports and notices about your account.", Product),
        new(Marketing, "News and offers", "Product news and offers from Mizan. Off until you turn it on.", NotificationChannels.None),
    ];

    private static readonly Dictionary<string, NotificationKindInfo> ByKind = All.ToDictionary(k => k.Kind, StringComparer.Ordinal);

    public static bool IsKnown(string? kind) => kind is not null && ByKind.ContainsKey(kind);

    public static NotificationKindInfo Info(string kind) =>
        ByKind.TryGetValue(kind, out var info) ? info : ByKind[Account];

    /// <summary>The kind a notification type belongs to. A type nobody listed is an account notice.</summary>
    public static string ForType(string? type) => type switch
    {
        "achievement_unlocked" or "streak_milestone" => Achievements,
        "evening_reminder" or "reminder" => Reminders,
        "meal_plan_drafted" or "meal_plan_accepted" or "meal_plan_declined" or "shopping_list_ready" => Planning,
        "trainer_request" or "trainer_request_response" or "trainer_relationship_ended" => Coaching,
        "household_invite" or "household_invite_response" => Household,
        "feed_comment" or "feed_reaction" or "follow_request" or "follow_accepted" or "content_report_actioned" => Social,
        "marketing" => Marketing,
        _ => Account,
    };

    /// <summary>The channel's name on the wire and in the database.</summary>
    public static string Name(NotificationChannels channel) => channel switch
    {
        NotificationChannels.InApp => "inApp",
        NotificationChannels.Telegram => "telegram",
        NotificationChannels.Push => "push",
        NotificationChannels.Email => "email",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "One channel at a time."),
    };

    public static IReadOnlyList<NotificationChannels> Each { get; } =
        [NotificationChannels.InApp, NotificationChannels.Telegram, NotificationChannels.Push, NotificationChannels.Email];
}
