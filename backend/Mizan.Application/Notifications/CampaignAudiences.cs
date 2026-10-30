using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;

namespace Mizan.Application.Notifications;

/// <summary>One person a campaign reaches, with the channels they get it on.</summary>
public sealed record CampaignRecipient(Guid UserId, string? Name, NotificationChannels Channels);

/// <summary>An audience resolved: everyone who passed the filters, and of them who the message reaches on which channels.</summary>
public sealed record CampaignAudience(int Matched, IReadOnlyList<CampaignRecipient> Recipients)
{
    public int Reachable(NotificationChannels channel) => Recipients.Count(r => r.Channels.HasFlag(channel));
}

/// <summary>
/// Who a campaign reaches (docs/ADMIN.md#notification-campaigns). The database narrows the
/// candidates, <see cref="CampaignAudienceRules"/> decides each one, and then each person's own
/// choices for the campaign's kind decide the channels, so marketing reaches only people who turned
/// it on. The preview, the confirmation, the scheduler and each delivery all come through here, so
/// the count an administrator confirms is the count that is staged.
/// </summary>
public sealed class CampaignAudiences
{
    private readonly IMizanDbContext _context;
    private readonly IEntitlementService _entitlements;
    private readonly IPushSender _push;
    private readonly TimeProvider _time;

    public CampaignAudiences(IMizanDbContext context, IEntitlementService entitlements, IPushSender push, TimeProvider time)
    {
        _context = context;
        _entitlements = entitlements;
        _push = push;
        _time = time;
    }

    /// <summary>The audience of <paramref name="filters"/> for a message of <paramref name="kind"/> on <paramref name="channels"/>, among <paramref name="only"/> when given.</summary>
    public async Task<CampaignAudience> ResolveAsync(
        NotificationAudience filters,
        string kind,
        NotificationChannels channels,
        IReadOnlyCollection<Guid>? only = null,
        CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var users = _context.Users.AsNoTracking().Where(u => !u.Banned || (u.BanExpires != null && u.BanExpires <= now));
        if (only is not null) users = users.Where(u => only.Contains(u.Id));
        if (filters.SignedUpFrom is { } from) users = users.Where(u => u.CreatedAt >= from);
        if (filters.SignedUpBefore is { } before) users = users.Where(u => u.CreatedAt < before);
        if (filters.HasHousehold is { } household) users = users.Where(u => u.HouseholdMemberships.Any() == household);
        if (filters.TelegramLinked is { } linked) users = users.Where(u => _context.TelegramLinks.Any(l => l.UserId == u.Id) == linked);

        var rows = await users
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.CreatedAt,
                u.TimeZoneId,
                u.Banned,
                u.BanExpires,
                u.NotifyByTelegram,
                Emailable = u.EmailVerified && u.Email != "",
                LastSeen = u.Sessions.Where(s => s.ImpersonatorId == null).Max(s => (DateTime?)s.LastSeenAt),
                LastLogged = u.Streaks.Max(s => s.LastActivityDate),
                Linked = _context.TelegramLinks.Any(l => l.UserId == u.Id),
                InHousehold = u.HouseholdMemberships.Any(),
                HasDevice = _context.DeviceTokens.Any(d => d.UserId == u.Id),
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return new CampaignAudience(0, []);

        var ids = rows.Select(r => r.Id).ToList();
        var pro = await _entitlements.GetProUserIdsAsync(ids, cancellationToken);
        var lifetime = (await _context.Subscriptions.AsNoTracking()
            .Where(s => s.IsLifetime && ids.Contains(s.UserId))
            .Select(s => s.UserId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var matched = rows
            .Where(r => CampaignAudienceRules.Matches(
                filters,
                new AudienceCandidate(
                    r.Id,
                    r.CreatedAt,
                    CampaignPlans.Of(pro.Contains(r.Id), lifetime.Contains(r.Id)),
                    CampaignAudienceRules.LastActive(r.CreatedAt, r.LastSeen, r.LastLogged),
                    r.Linked,
                    r.InHousehold,
                    r.TimeZoneId,
                    r.Banned,
                    r.BanExpires),
                now))
            .ToList();
        if (matched.Count == 0) return new CampaignAudience(0, []);

        var matchedIds = matched.Select(r => r.Id).ToList();
        var preferences = await _context.NotificationPreferences.AsNoTracking()
            .Where(p => p.Kind == kind && matchedIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);
        var pushReady = await _push.IsConfiguredAsync(cancellationToken);

        var recipients = matched
            .Select(r =>
            {
                var allowed = channels & NotificationPreferenceRules.Allowed(kind, preferences.GetValueOrDefault(r.Id));
                // Each channel's own conditions, as the notification writer and the messenger apply them.
                if (!(r.NotifyByTelegram && r.Linked && pro.Contains(r.Id))) allowed &= ~NotificationChannels.Telegram;
                if (!r.Emailable) allowed &= ~NotificationChannels.Email;
                if (!(pushReady && r.HasDevice)) allowed &= ~NotificationChannels.Push;
                return new CampaignRecipient(r.Id, r.Name, allowed);
            })
            .Where(r => r.Channels != NotificationChannels.None)
            .ToList();

        return new CampaignAudience(matched.Count, recipients);
    }

    /// <summary>When each of <paramref name="userIds"/> was last active, by the audience rules' measure.</summary>
    /// <remarks>With <paramref name="notAfter"/>, only people with no activity after it are read: nobody else can have their last activity before it.</remarks>
    public async Task<Dictionary<Guid, DateTime>> LastActiveAsync(IReadOnlyCollection<Guid>? userIds, DateTime? notAfter, CancellationToken cancellationToken)
    {
        var users = _context.Users.AsNoTracking();
        if (userIds is not null) users = users.Where(u => userIds.Contains(u.Id));
        if (notAfter is { } end)
        {
            var day = DateOnly.FromDateTime(end);
            users = users.Where(u => u.CreatedAt <= end
                && !u.Sessions.Any(s => s.ImpersonatorId == null && s.LastSeenAt > end)
                && !u.Streaks.Any(s => s.LastActivityDate > day));
        }

        var rows = await users
            .Select(u => new
            {
                u.Id,
                u.CreatedAt,
                LastSeen = u.Sessions.Where(s => s.ImpersonatorId == null).Max(s => (DateTime?)s.LastSeenAt),
                LastLogged = u.Streaks.Max(s => s.LastActivityDate),
            })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Id, r => CampaignAudienceRules.LastActive(r.CreatedAt, r.LastSeen, r.LastLogged));
    }
}

/// <summary>
/// Stages a campaign's deliveries: one <c>notification-delivery</c> job per occurrence, recipient
/// and channel, keyed as every schedule's are, and paced per channel (<see cref="CampaignRules.PerMinute"/>)
/// so a large audience goes out over the minutes after the occurrence instead of at once.
/// </summary>
public sealed class CampaignStager
{
    private readonly IOutbox _outbox;

    public CampaignStager(IOutbox outbox) => _outbox = outbox;

    /// <summary>Stages each recipient's deliveries of one occurrence. The caller saves.</summary>
    public async Task<int> StageAsync(NotificationSchedule schedule, DateTimeOffset occurrence, IEnumerable<CampaignRecipient> recipients, CancellationToken cancellationToken)
    {
        var index = new Dictionary<NotificationChannels, int>();
        var staged = 0;
        foreach (var recipient in recipients)
        {
            foreach (var channel in NotificationKinds.Each.Where(c => recipient.Channels.HasFlag(c) && schedule.Channels.HasFlag(c)))
            {
                var position = index.GetValueOrDefault(channel);
                index[channel] = position + 1;
                var name = NotificationKinds.Name(channel);
                await _outbox.ScheduleAsync(
                    OutboxJobTypes.NotificationDelivery,
                    new NotificationDeliveryJob(schedule.Id, recipient.UserId, occurrence.UtcDateTime, name),
                    CampaignRules.RunAfter(occurrence.UtcDateTime, position, channel),
                    NotificationScheduler.DeliveryKey(schedule.Id, occurrence, recipient.UserId, name),
                    cancellationToken);
                staged++;
            }
        }
        return staged;
    }
}

/// <summary>
/// The events a lifecycle message follows (docs/ADMIN.md#notification-campaigns). Each scan finds the
/// people whose event, moved by the schedule's offset, falls between an hour ago and the scheduler's
/// lookahead, keeps those the audience filters and their own choices allow, and stages their
/// deliveries under the usual key, so finding the same person again adds nothing. An occurrence
/// before the campaign was started is not sent. At send time <see cref="HoldsAsync"/> asks whether
/// the event is still the one the delivery was staged for.
/// </summary>
public abstract class CampaignEventSource : INotificationEventSource
{
    protected CampaignEventSource(IMizanDbContext context, CampaignAudiences audiences, CampaignStager stager)
    {
        Context = context;
        Audiences = audiences;
        Stager = stager;
    }

    protected IMizanDbContext Context { get; }
    protected CampaignAudiences Audiences { get; }
    private CampaignStager Stager { get; }

    public abstract string Event { get; }

    /// <summary>Each person whose event happened (or happens) between <paramref name="from"/> and <paramref name="to"/>, with its instant.</summary>
    protected abstract Task<IReadOnlyDictionary<Guid, DateTime>> EventsAsync(DateTime from, DateTime to, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken);

    public async Task<int> StageAsync(NotificationSchedule schedule, DateTimeOffset now, DateTimeOffset horizon, CancellationToken cancellationToken)
    {
        if (schedule.Campaign is not { Status: CampaignStatus.Active } campaign) return 0;
        var offset = TimeSpan.FromMinutes(schedule.Trigger.OffsetMinutes);
        var earliest = now - NotificationScheduler.Grace;
        if (campaign.ActivatedAt is { } activated && activated > earliest.UtcDateTime) earliest = new DateTimeOffset(DateTime.SpecifyKind(activated, DateTimeKind.Utc));

        var events = await EventsAsync((earliest - offset).UtcDateTime, (horizon - offset).UtcDateTime, null, cancellationToken);
        if (events.Count == 0) return 0;

        var audience = await Audiences.ResolveAsync(schedule.Audience, schedule.Message.Kind, schedule.Channels, events.Keys.ToList(), cancellationToken);
        var staged = 0;
        foreach (var group in audience.Recipients.GroupBy(r => CampaignTriggerRules.Occurrence(events[r.UserId], schedule.Trigger.OffsetMinutes)))
        {
            if (group.Key < earliest.AddMinutes(-1) || group.Key > horizon) continue;
            staged += await Stager.StageAsync(schedule, group.Key, group, cancellationToken);
        }
        return staged;
    }

    /// <summary>Whether the person's event still puts an occurrence at <paramref name="occurrence"/>.</summary>
    public async Task<bool> HoldsAsync(NotificationSchedule schedule, Guid userId, DateTime occurrence, CancellationToken cancellationToken)
    {
        var at = new DateTimeOffset(DateTime.SpecifyKind(occurrence, DateTimeKind.Utc));
        var offset = TimeSpan.FromMinutes(schedule.Trigger.OffsetMinutes);
        var events = await EventsAsync((at - offset).AddDays(-1).UtcDateTime, (at - offset).AddDays(1).UtcDateTime, [userId], cancellationToken);
        return events.TryGetValue(userId, out var eventAt) && CampaignTriggerRules.Occurrence(eventAt, schedule.Trigger.OffsetMinutes) == at;
    }
}

/// <summary><c>user.signed_up</c>: the account's creation.</summary>
public sealed class SignUpEvents : CampaignEventSource
{
    public SignUpEvents(IMizanDbContext context, CampaignAudiences audiences, CampaignStager stager) : base(context, audiences, stager) { }

    public override string Event => CampaignEvents.SignedUp;

    protected override async Task<IReadOnlyDictionary<Guid, DateTime>> EventsAsync(DateTime from, DateTime to, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken)
    {
        var users = Context.Users.AsNoTracking().Where(u => u.CreatedAt >= from && u.CreatedAt <= to);
        if (only is not null) users = users.Where(u => only.Contains(u.Id));
        return await users.ToDictionaryAsync(u => u.Id, u => u.CreatedAt, cancellationToken);
    }
}

/// <summary><c>user.inactive</c>: the person's last visit or logged day, from which the quiet days are counted.</summary>
public sealed class InactivityEvents : CampaignEventSource
{
    public InactivityEvents(IMizanDbContext context, CampaignAudiences audiences, CampaignStager stager) : base(context, audiences, stager) { }

    public override string Event => CampaignEvents.Inactive;

    protected override async Task<IReadOnlyDictionary<Guid, DateTime>> EventsAsync(DateTime from, DateTime to, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken)
    {
        var last = await Audiences.LastActiveAsync(only, only is null ? to : null, cancellationToken);
        return last.Where(p => p.Value >= from && p.Value <= to).ToDictionary(p => p.Key, p => p.Value);
    }
}

/// <summary><c>subscription.pro_ends</c>: the end of a Pro subscription that was cancelled or set to cancel.</summary>
public sealed class ProEndEvents : CampaignEventSource
{
    public ProEndEvents(IMizanDbContext context, CampaignAudiences audiences, CampaignStager stager) : base(context, audiences, stager) { }

    public override string Event => CampaignEvents.ProEnds;

    protected override async Task<IReadOnlyDictionary<Guid, DateTime>> EventsAsync(DateTime from, DateTime to, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken)
    {
        var subscriptions = Context.Subscriptions.AsNoTracking().Where(s => !s.IsLifetime && (s.CurrentPeriodEnd != null || s.ScheduledChangeAt != null));
        if (only is not null) subscriptions = subscriptions.Where(s => only.Contains(s.UserId));
        var rows = await subscriptions
            .Select(s => new { s.UserId, s.IsLifetime, s.Status, s.ScheduledChangeAction, s.ScheduledChangeAt, s.CurrentPeriodEnd })
            .ToListAsync(cancellationToken);

        var ends = new Dictionary<Guid, DateTime>();
        foreach (var row in rows)
        {
            if (CampaignTriggerRules.ProEndsAt(row.IsLifetime, row.Status, row.ScheduledChangeAction, row.ScheduledChangeAt, row.CurrentPeriodEnd) is not { } end) continue;
            if (end < from || end > to) continue;
            if (!ends.TryGetValue(row.UserId, out var known) || end > known) ends[row.UserId] = end;
        }
        return ends;
    }
}
