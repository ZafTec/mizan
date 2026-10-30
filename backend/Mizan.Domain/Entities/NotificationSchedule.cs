using Mizan.Domain.Notifications;

namespace Mizan.Domain.Entities;

public enum NotificationScheduleOwner
{
    /// <summary>A person's own schedule, such as a reminder they set.</summary>
    User = 0,

    /// <summary>The platform's: built-in schedules and, later, administrators' campaigns.</summary>
    Platform = 1,
}

public enum NotificationAudienceKind
{
    /// <summary>One person, <see cref="NotificationAudience.UserId"/>.</summary>
    User = 0,

    /// <summary>
    /// Whoever the trigger's event happens to. Only an event trigger can use it, because only the
    /// event says who.
    /// </summary>
    EventSubjects = 1,

    /// <summary>
    /// Everyone who passes the filters (docs/ADMIN.md#notification-campaigns). On an event trigger the
    /// filters narrow the people the event happens to; on a timed one they are the audience.
    /// </summary>
    Segment = 2,
}

/// <summary>Who a schedule reaches. Stored as JSON so later audience kinds add fields, not tables.</summary>
public class NotificationAudience
{
    public NotificationAudienceKind Kind { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>Segment: <c>free</c>, <c>pro</c>, <c>lifetime</c> (<see cref="CampaignPlans"/>); empty is every plan.</summary>
    public List<string> Plans { get; set; } = [];

    /// <summary>Segment: signed up on or after this instant (UTC).</summary>
    public DateTime? SignedUpFrom { get; set; }

    /// <summary>Segment: signed up before this instant (UTC).</summary>
    public DateTime? SignedUpBefore { get; set; }

    /// <summary>Segment: nothing logged and no visit for at least this many days.</summary>
    public int? InactiveDays { get; set; }

    /// <summary>Segment: true for people with a linked Telegram chat, false for people without; null for both.</summary>
    public bool? TelegramLinked { get; set; }

    /// <summary>Segment: true for members of a household, false for people in none; null for both.</summary>
    public bool? HasHousehold { get; set; }

    /// <summary>Segment: IANA time zones; empty is every zone. A person who never set one is in <c>UTC</c>.</summary>
    public List<string> TimeZones { get; set; } = [];

    public static NotificationAudience Person(Guid userId) => new() { Kind = NotificationAudienceKind.User, UserId = userId };

    public static NotificationAudience OfEvent() => new() { Kind = NotificationAudienceKind.EventSubjects };
}

/// <summary>What a schedule sends: the notification's type, kind and structured content.</summary>
public class NotificationTemplate
{
    public string Type { get; set; } = string.Empty;
    public string Kind { get; set; } = NotificationKinds.Account;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public List<NotificationActionItem> Actions { get; set; } = [];
    public List<NotificationFactItem> Facts { get; set; } = [];
}

/// <summary>
/// What makes a person's schedule a reminder (docs/ARCHITECTURE.md#reminders): its kind and what it
/// is tied to. Stored as JSON beside the trigger; null on every other schedule.
/// </summary>
public class ReminderDetails
{
    /// <summary>One of <see cref="ReminderKinds"/>.</summary>
    public string Kind { get; set; } = ReminderKinds.Custom;

    public Guid? WorkoutTemplateId { get; set; }
    public Guid? MealPlanId { get; set; }
    public Guid? PrepSessionId { get; set; }

    /// <summary>Pre-cook: when the prep starts on the person's clock, and how long before it to remind.</summary>
    public TimeOnly? PrepTime { get; set; }
    public int? LeadMinutes { get; set; }

    /// <summary>Meal log: the meal it is for, such as <c>BREAKFAST</c>.</summary>
    public string? MealType { get; set; }
}

/// <summary>
/// What makes a platform schedule an administrator's campaign or lifecycle message
/// (docs/ADMIN.md#notification-campaigns). Stored as JSON beside the trigger; null on every other schedule.
/// <see cref="NotificationSchedule.Enabled"/> is true exactly while <see cref="Status"/> is active.
/// </summary>
public class NotificationCampaign
{
    public string Name { get; set; } = string.Empty;
    public CampaignPurpose Purpose { get; set; }
    public CampaignStatus Status { get; set; }

    /// <summary>The zone a recurring campaign's local time is read in; null is UTC.</summary>
    public string? TimeZoneId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    /// <summary>The audience count the administrator confirmed when they started it.</summary>
    public int? ConfirmedAudience { get; set; }

    public DateTime? ActivatedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

/// <summary>
/// The one kind of schedule every timed notice uses (docs/ARCHITECTURE.md#notifications): who owns
/// it, who it reaches, what it says, on which channels and when. The scheduler stages one outbox
/// job per occurrence, recipient and channel, keyed so a second scan or a retry adds nothing.
/// <see cref="NextRunAt"/> is when it next fires (for an event trigger, null: its source says);
/// <see cref="LastRunAt"/> is the last occurrence staged.
/// </summary>
public class NotificationSchedule
{
    public Guid Id { get; set; }

    /// <summary>A built-in schedule's stable name, such as <c>evening-reminder</c>; null for others.</summary>
    public string? Key { get; set; }

    public NotificationScheduleOwner Owner { get; set; }
    public Guid? OwnerUserId { get; set; }
    public NotificationAudience Audience { get; set; } = new();
    public NotificationTemplate Message { get; set; } = new();

    /// <summary>The channels it may use. Each person's own choices still apply on top.</summary>
    public NotificationChannels Channels { get; set; } = NotificationChannels.All;

    public NotificationTrigger Trigger { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }

    /// <summary>Deliveries of occurrences before this instant are dropped: a reminder skipped or done for today.</summary>
    public DateTime? SkipUntil { get; set; }

    /// <summary>Set on a person's own reminder; null on every other schedule.</summary>
    public ReminderDetails? Reminder { get; set; }

    /// <summary>Set on an administrator's campaign or lifecycle message; null on every other schedule.</summary>
    public NotificationCampaign? Campaign { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User? OwnerUser { get; set; }
}
