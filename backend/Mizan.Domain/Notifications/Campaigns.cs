using System.Globalization;
using System.Text.RegularExpressions;
using Mizan.Domain.Entities;

namespace Mizan.Domain.Notifications;

/// <summary>Why an administrator sends a campaign. It decides the notification kind and so who may receive it.</summary>
public enum CampaignPurpose
{
    /// <summary>News and offers: kind <c>marketing</c>, which reaches only people who turned it on.</summary>
    Marketing = 0,

    /// <summary>A product message tied to the person's use of Mizan, such as a welcome: kind <c>account</c>.</summary>
    Lifecycle = 1,
}

/// <summary>Where a campaign stands. Only an active campaign stages or sends anything.</summary>
public enum CampaignStatus
{
    Draft = 0,
    Active = 1,
    Paused = 2,
    Cancelled = 3,
}

/// <summary>What an administrator does to a campaign.</summary>
public enum CampaignAction
{
    Edit,
    Activate,
    Pause,
    Resume,
    Cancel,
}

/// <summary>The plans an audience can name.</summary>
public static class CampaignPlans
{
    public const string Free = "free";
    public const string Pro = "pro";
    public const string Lifetime = "lifetime";

    public static IReadOnlyList<string> All { get; } = [Free, Pro, Lifetime];

    /// <summary>A lifetime purchase is its own plan; any other Pro entitlement is <c>pro</c>.</summary>
    public static string Of(bool isPro, bool isLifetime) => isLifetime ? Lifetime : isPro ? Pro : Free;
}

/// <summary>The events a lifecycle message can follow, with an offset in days.</summary>
public static class CampaignEvents
{
    /// <summary>The person signed up. The offset is days after.</summary>
    public const string SignedUp = "user.signed_up";

    /// <summary>The person's last visit or log. The offset is the quiet days after it.</summary>
    public const string Inactive = "user.inactive";

    /// <summary>A Pro subscription that will not renew reaches its end. The offset is days before.</summary>
    public const string ProEnds = "subscription.pro_ends";

    public static IReadOnlyList<string> All { get; } = [SignedUp, Inactive, ProEnds];

    public static bool IsKnown(string? name) => name is not null && All.Contains(name);
}

/// <summary>One person as the audience rules see them.</summary>
/// <param name="Plan">One of <see cref="CampaignPlans"/>.</param>
/// <param name="LastActiveAt">The later of their last visit and their last logged day; their sign-up when there is neither.</param>
public sealed record AudienceCandidate(
    Guid UserId,
    DateTime CreatedAt,
    string Plan,
    DateTime LastActiveAt,
    bool TelegramLinked,
    bool HasHousehold,
    string? TimeZoneId,
    bool Banned = false,
    DateTime? BanExpires = null);

/// <summary>
/// Who a campaign's audience is (docs/ADMIN.md#notification-campaigns). The database narrows the
/// candidates and these rules decide each one, so the preview count and the send agree. A suspended
/// account is never in an audience.
/// </summary>
public static class CampaignAudienceRules
{
    public const int MaxInactiveDays = 365;
    public const int MaxTimeZones = 50;
    public const string DefaultZone = "UTC";

    /// <summary>Why the filters cannot be used, or null when they can.</summary>
    public static string? Check(NotificationAudience audience)
    {
        if (audience.Kind != NotificationAudienceKind.Segment) return "A campaign's audience is a set of filters.";
        foreach (var plan in audience.Plans)
        {
            if (!CampaignPlans.All.Contains(plan)) return $"\"{plan}\" is not a plan. Use free, pro or lifetime.";
        }
        if (audience.SignedUpFrom is { } from && audience.SignedUpBefore is { } before && from >= before)
            return "The sign-up range ends before it starts.";
        if (audience.InactiveDays is { } days && days is < 1 or > MaxInactiveDays)
            return $"Inactive days is between 1 and {MaxInactiveDays}.";
        if (audience.TimeZones.Count > MaxTimeZones) return $"Choose at most {MaxTimeZones} time zones.";
        foreach (var zone in audience.TimeZones)
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _)) return $"\"{zone}\" is not a time zone.";
        }
        return null;
    }

    public static bool Matches(NotificationAudience audience, AudienceCandidate person, DateTime now)
    {
        if (person.Banned && (person.BanExpires is null || person.BanExpires > now)) return false;
        if (audience.Plans.Count > 0 && !audience.Plans.Contains(person.Plan)) return false;
        if (audience.SignedUpFrom is { } from && person.CreatedAt < from) return false;
        if (audience.SignedUpBefore is { } before && person.CreatedAt >= before) return false;
        if (audience.InactiveDays is { } days && person.LastActiveAt > now.AddDays(-days)) return false;
        if (audience.TelegramLinked is { } linked && person.TelegramLinked != linked) return false;
        if (audience.HasHousehold is { } household && person.HasHousehold != household) return false;
        if (audience.TimeZones.Count > 0 && !audience.TimeZones.Contains(person.TimeZoneId ?? DefaultZone, StringComparer.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>When a person was last active: the latest of sign-up, last visit and the start of their last logged day.</summary>
    public static DateTime LastActive(DateTime createdAt, DateTime? lastSeenAt, DateOnly? lastLoggedDay)
    {
        var latest = createdAt;
        if (lastSeenAt is { } seen && seen > latest) latest = seen;
        if (lastLoggedDay is { } day && day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) is var logged && logged > latest) latest = logged;
        return DateTime.SpecifyKind(latest, DateTimeKind.Utc);
    }

    /// <summary>The filters in words, for the list and the confirmation step.</summary>
    public static string Describe(NotificationAudience audience)
    {
        var parts = new List<string>();
        if (audience.Plans.Count > 0)
            parts.Add(string.Join(" or ", audience.Plans.Select(p => p switch { CampaignPlans.Pro => "Pro", CampaignPlans.Lifetime => "lifetime", _ => "Free" })));
        if (audience.SignedUpFrom is { } from && audience.SignedUpBefore is { } before)
            parts.Add($"signed up {Day(from)} to {Day(before.AddDays(-1))}");
        else if (audience.SignedUpFrom is { } onlyFrom)
            parts.Add($"signed up from {Day(onlyFrom)}");
        else if (audience.SignedUpBefore is { } onlyBefore)
            parts.Add($"signed up before {Day(onlyBefore)}");
        if (audience.InactiveDays is { } days) parts.Add(days == 1 ? "inactive for a day" : $"inactive for {days} days");
        if (audience.TelegramLinked is { } linked) parts.Add(linked ? "Telegram linked" : "no Telegram");
        if (audience.HasHousehold is { } household) parts.Add(household ? "in a household" : "in no household");
        if (audience.TimeZones.Count > 0) parts.Add(string.Join(", ", audience.TimeZones));
        if (parts.Count == 0) return "Everyone";
        var text = string.Join("; ", parts);
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Day(DateTime value) => value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}

/// <summary>
/// When a campaign fires. A timed campaign uses the reminder timings (once, daily, days, every) on
/// the campaign's own clock; a lifecycle message follows one of <see cref="CampaignEvents"/> by a
/// number of days, after it or, for Pro ending, before it.
/// </summary>
public static class CampaignTriggerRules
{
    public const int MaxEventDays = 365;
    private const int MinutesPerDay = 24 * 60;

    public static ReminderTriggerResult ForEvent(string? name, int days)
    {
        if (!CampaignEvents.IsKnown(name)) return ReminderTriggerResult.Fail("The event is user.signed_up, user.inactive or subscription.pro_ends.");
        if (days is < 0 or > MaxEventDays) return ReminderTriggerResult.Fail($"Days is between 0 and {MaxEventDays}.");
        if (name == CampaignEvents.Inactive && days < 1) return ReminderTriggerResult.Fail("Inactive needs at least one quiet day.");
        var offset = days * MinutesPerDay;
        return new(NotificationTrigger.AfterEvent(name!, name == CampaignEvents.ProEnds ? -offset : offset), null);
    }

    /// <summary>The days the trigger's offset stands for.</summary>
    public static int EventDays(NotificationTrigger trigger) => Math.Abs(trigger.OffsetMinutes) / MinutesPerDay;

    /// <summary>An event's occurrence: the event's instant plus the offset, to the minute, which is what the delivery key holds.</summary>
    public static DateTimeOffset Occurrence(DateTime eventAt, int offsetMinutes)
    {
        var at = new DateTimeOffset(DateTime.SpecifyKind(eventAt, DateTimeKind.Utc)).AddMinutes(offsetMinutes);
        return new DateTimeOffset(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// When a person's Pro ends, if it is going to: a subscription cancelled or set to cancel, at the end
    /// of the period it was paid for. A lifetime purchase and a subscription that renews never end.
    /// </summary>
    public static DateTime? ProEndsAt(bool isLifetime, string status, string? scheduledChangeAction, DateTime? scheduledChangeAt, DateTime? currentPeriodEnd)
    {
        if (isLifetime) return null;
        if (string.Equals(scheduledChangeAction, "cancel", StringComparison.OrdinalIgnoreCase)) return scheduledChangeAt ?? currentPeriodEnd;
        if (string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase)) return currentPeriodEnd;
        return null;
    }

    public static string Describe(NotificationTrigger trigger, TimeZoneInfo zone)
    {
        if (trigger.Kind != NotificationTriggerKind.EventOffset) return ReminderRules.Describe(trigger, zone);
        var days = EventDays(trigger);
        var span = days == 1 ? "1 day" : $"{days} days";
        return trigger.Event switch
        {
            CampaignEvents.SignedUp => days == 0 ? "When someone signs up" : $"{span} after sign-up",
            CampaignEvents.Inactive => $"After {span} without activity",
            CampaignEvents.ProEnds => days == 0 ? "When Pro ends" : $"{span} before Pro ends",
            _ => "When the event happens",
        };
    }
}

/// <summary>
/// The rules of a campaign's life and delivery (docs/ADMIN.md#notification-campaigns): what each
/// action may do from each status, the notification kind its purpose sends, and the pace each
/// channel is sent at.
/// </summary>
public static class CampaignRules
{
    public const int MaxNameLength = 120;
    public const int MaxTitleLength = 150;
    public const int MaxBodyLength = 500;

    public static string KindFor(CampaignPurpose purpose) =>
        purpose == CampaignPurpose.Marketing ? NotificationKinds.Marketing : NotificationKinds.Account;

    public const string NotificationType = "campaign";

    /// <summary>The status an action leads to, or why it cannot be taken.</summary>
    public static (CampaignStatus? Next, string? Error) Apply(CampaignStatus from, CampaignAction action) => (action, from) switch
    {
        (CampaignAction.Edit, CampaignStatus.Draft or CampaignStatus.Paused) => (from, null),
        (CampaignAction.Edit, CampaignStatus.Active) => (null, "Pause the campaign before you change it."),
        (CampaignAction.Activate, CampaignStatus.Draft) => (CampaignStatus.Active, null),
        (CampaignAction.Activate, _) => (null, "Only a draft can be started."),
        (CampaignAction.Pause, CampaignStatus.Active) => (CampaignStatus.Paused, null),
        (CampaignAction.Pause, _) => (null, "Only an active campaign can be paused."),
        (CampaignAction.Resume, CampaignStatus.Paused) => (CampaignStatus.Active, null),
        (CampaignAction.Resume, _) => (null, "Only a paused campaign can be resumed."),
        (CampaignAction.Cancel, CampaignStatus.Draft or CampaignStatus.Active or CampaignStatus.Paused) => (CampaignStatus.Cancelled, null),
        (_, CampaignStatus.Cancelled) => (null, "The campaign is cancelled."),
        _ => (null, "That cannot be done to the campaign now."),
    };

    /// <summary>
    /// What the list shows: <c>draft</c>, <c>scheduled</c> (a next send is due), <c>live</c> (an
    /// event rule waiting for people), <c>sent</c> (nothing left to stage), <c>paused</c> or <c>cancelled</c>.
    /// </summary>
    public static string Phase(CampaignStatus status, NotificationTriggerKind trigger, DateTime? nextRunAt) => status switch
    {
        CampaignStatus.Draft => "draft",
        CampaignStatus.Paused => "paused",
        CampaignStatus.Cancelled => "cancelled",
        _ when trigger == NotificationTriggerKind.EventOffset => "live",
        _ when nextRunAt is not null => "scheduled",
        _ => "sent",
    };

    /// <summary>How many deliveries a channel takes a minute. A larger audience is spread over the minutes after the occurrence.</summary>
    public static int PerMinute(NotificationChannels channel) => channel switch
    {
        NotificationChannels.InApp => 1000,
        NotificationChannels.Push => 600,
        // Telegram allows about 30 messages a second for one bot; stay well below it.
        NotificationChannels.Telegram => 1200,
        NotificationChannels.Email => 120,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "One channel at a time."),
    };

    /// <summary>When the <paramref name="index"/>th delivery of an occurrence on a channel may run.</summary>
    public static DateTime RunAfter(DateTime occurrence, int index, NotificationChannels channel) =>
        DateTime.SpecifyKind(occurrence, DateTimeKind.Utc).AddMinutes(index / PerMinute(channel));
}

/// <summary>
/// The placeholders a campaign's text may hold: <c>{firstName}</c>, the first word of the person's
/// name (or "there"), and <c>{appLink}</c>, the app's address. Each recipient gets their own.
/// </summary>
public static partial class NotificationPlaceholders
{
    public const string FirstName = "{firstName}";
    public const string AppLink = "{appLink}";
    public const string NoName = "there";

    public static string FirstNameOf(string? name) =>
        name?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() is { Length: > 0 } first ? first : NoName;

    public static string? Render(string? text, string firstName, string appLink) =>
        text?.Replace(FirstName, firstName, StringComparison.Ordinal).Replace(AppLink, appLink, StringComparison.Ordinal);

    /// <summary>The placeholders in <paramref name="text"/> that are not one of the two above.</summary>
    public static IReadOnlyList<string> Unknown(string? text) =>
        text is null
            ? []
            : Placeholder().Matches(text).Select(m => m.Value).Where(v => v is not FirstName and not AppLink).Distinct().ToList();

    [GeneratedRegex(@"\{[A-Za-z]+\}")]
    private static partial Regex Placeholder();
}

/// <summary>What a channel's outbox jobs say about one campaign: waiting, done, or given up.</summary>
public sealed record JobCounts(int Pending, int Succeeded, int Failed)
{
    public static JobCounts None { get; } = new(0, 0, 0);
    public int Total => Pending + Succeeded + Failed;
}

/// <summary>One channel's line of a delivery report. <see cref="Opened"/> is null where the channel does not say.</summary>
public sealed record CampaignChannelTally(NotificationChannels Channel, int Queued, int Sent, int Failed, int Skipped, int? Opened);

/// <summary>
/// A campaign's delivery report per channel. Each delivery job becomes a channel's own job (or, in the
/// app, a row) unless the person's choices leave it out, which is <c>skipped</c>. Queued is work still
/// waiting at either step, sent is the channel's job done or the row written, failed is a job that gave
/// up. Only the app reports opened: its rows that were read.
/// </summary>
public static class CampaignReport
{
    public static CampaignChannelTally Tally(NotificationChannels channel, JobCounts deliveries, JobCounts channelJobs, int rows = 0, int read = 0)
    {
        if (channel == NotificationChannels.InApp)
            return new(channel, deliveries.Pending, rows, deliveries.Failed, Math.Max(0, deliveries.Succeeded - rows), read);

        return new(
            channel,
            deliveries.Pending + channelJobs.Pending,
            channelJobs.Succeeded,
            deliveries.Failed + channelJobs.Failed,
            Math.Max(0, deliveries.Succeeded - channelJobs.Total),
            null);
    }
}
