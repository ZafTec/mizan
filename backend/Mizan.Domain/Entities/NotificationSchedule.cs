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
    /// event says who; segments for timed campaigns are added beside it.
    /// </summary>
    EventSubjects = 1,
}

/// <summary>Who a schedule reaches. Stored as JSON so later audience kinds add fields, not tables.</summary>
public class NotificationAudience
{
    public NotificationAudienceKind Kind { get; set; }
    public Guid? UserId { get; set; }

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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User? OwnerUser { get; set; }
}
