using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Notifications;

/// <summary>
/// Where an event trigger's occurrences come from (docs/ARCHITECTURE.md#notifications). The evening
/// reminder is one: its event is a plan's eating window closing, and its recipients are the people who
/// follow that plan. A source stages its own outbox jobs, keyed so a scan that finds an occurrence again
/// adds nothing, and decides at send time whether the occurrence still holds.
/// </summary>
public interface INotificationEventSource
{
    /// <summary>The trigger's <see cref="NotificationTrigger.Event"/> this source answers for.</summary>
    string Event { get; }

    /// <summary>Stages the schedule's occurrences due before <paramref name="horizon"/>. Returns how many it found. The caller saves.</summary>
    Task<int> StageAsync(NotificationSchedule schedule, DateTimeOffset now, DateTimeOffset horizon, CancellationToken cancellationToken);

    /// <summary>Whether the event still puts the person's occurrence at <paramref name="occurrence"/>, asked when a staged delivery is sent.</summary>
    Task<bool> HoldsAsync(NotificationSchedule schedule, Guid userId, DateTime occurrence, CancellationToken cancellationToken) => Task.FromResult(true);
}

/// <summary>A queued delivery of one occurrence of a timed schedule to one person on one channel.</summary>
public sealed record NotificationDeliveryJob(Guid ScheduleId, Guid UserId, DateTime Occurrence, string Channel);

/// <summary>
/// The one scheduler for notifications that go out at a time rather than when something happens
/// (docs/ARCHITECTURE.md#notifications). Each scan stages the occurrences due within
/// <see cref="Lookahead"/> as outbox jobs that wait until their time. A timed schedule's job is keyed by
/// schedule, occurrence, recipient and channel, so a scan repeated after a failure, or by a second
/// instance, adds nothing. An occurrence missed by more than <see cref="Grace"/> (the service was down)
/// is skipped rather than sent late.
/// </summary>
public sealed class NotificationScheduler
{
    /// <summary>The built-in schedule that carries the evening reminder.</summary>
    public const string EveningReminderKey = "evening-reminder";

    /// <summary>The event of a plan's eating window closing, which the evening reminder is offset from.</summary>
    public const string EatingWindowEndEvent = "plan.eating_window_end";

    public static readonly TimeSpan Lookahead = TimeSpan.FromHours(2);
    public static readonly TimeSpan Grace = TimeSpan.FromHours(1);

    private readonly IMizanDbContext _context;
    private readonly IOutbox _outbox;
    private readonly IEnumerable<INotificationEventSource> _sources;
    private readonly CampaignAudiences _audiences;
    private readonly CampaignStager _stager;
    private readonly TimeProvider _time;

    public NotificationScheduler(
        IMizanDbContext context,
        IOutbox outbox,
        IEnumerable<INotificationEventSource> sources,
        CampaignAudiences audiences,
        CampaignStager stager,
        TimeProvider time)
    {
        _context = context;
        _outbox = outbox;
        _sources = sources;
        _audiences = audiences;
        _stager = stager;
        _time = time;
    }

    /// <summary>
    /// Stages one schedule's occurrences that are due now, without waiting for the next scan: how a
    /// campaign sent now goes out at once. The caller saves.
    /// </summary>
    public Task<int> StageDueAsync(NotificationSchedule schedule, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        return schedule.NextRunAt is { } next && next <= (now + Lookahead).UtcDateTime
            ? StageTimedAsync(schedule, now, now + Lookahead, cancellationToken)
            : Task.FromResult(0);
    }

    /// <summary>One scan of every enabled schedule. Returns how many occurrences it found.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await EnsureBuiltInAsync(cancellationToken);

        var now = _time.GetUtcNow();
        var horizon = now + Lookahead;
        var found = 0;

        var timed = await _context.NotificationSchedules
            .Where(s => s.Enabled && s.NextRunAt != null && s.NextRunAt <= horizon.UtcDateTime)
            .ToListAsync(cancellationToken);
        // A finished reminder also has no next run; only schedules without one can be event triggers.
        var events = (await _context.NotificationSchedules.Where(s => s.Enabled && s.NextRunAt == null && s.Reminder == null).ToListAsync(cancellationToken))
            .Where(s => s.Trigger.Kind == NotificationTriggerKind.EventOffset)
            .ToList();

        foreach (var schedule in timed)
            found += await StageTimedAsync(schedule, now, horizon, cancellationToken);

        foreach (var schedule in events)
        {
            var source = _sources.FirstOrDefault(s => s.Event == schedule.Trigger.Event);
            if (source is null) continue;
            if (await source.StageAsync(schedule, now, horizon, cancellationToken) is var staged and > 0)
            {
                found += staged;
                schedule.LastRunAt = now.UtcDateTime;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return found;
    }

    /// <summary>Whether a schedule is still there and enabled, which a delivery checks before it sends.</summary>
    public Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken) =>
        _context.NotificationSchedules.AsNoTracking().AnyAsync(s => s.Key == key && s.Enabled, cancellationToken);

    private async Task<int> StageTimedAsync(NotificationSchedule schedule, DateTimeOffset now, DateTimeOffset horizon, CancellationToken cancellationToken)
    {
        if (schedule.Campaign is not null) return await StageCampaignAsync(schedule, now, horizon, cancellationToken);
        var recipients = await RecipientsAsync(schedule, cancellationToken);
        var zoneId = recipients.Count == 1 ? recipients[0].TimeZoneId : null;
        var zone = StreakClock.Zone(zoneId);

        var staged = 0;
        var at = (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(schedule.NextRunAt!.Value, DateTimeKind.Utc));
        while (at is { } occurrence && occurrence <= horizon)
        {
            if (occurrence >= now - Grace)
            {
                foreach (var recipient in recipients)
                {
                    foreach (var channel in NotificationKinds.Each.Where(c => schedule.Channels.HasFlag(c)))
                    {
                        var name = NotificationKinds.Name(channel);
                        await _outbox.ScheduleAsync(
                            OutboxJobTypes.NotificationDelivery,
                            new NotificationDeliveryJob(schedule.Id, recipient.Id, occurrence.UtcDateTime, name),
                            occurrence.UtcDateTime,
                            DeliveryKey(schedule.Id, occurrence, recipient.Id, name),
                            cancellationToken);
                    }
                }
                schedule.LastRunAt = occurrence.UtcDateTime;
                staged++;
            }
            at = NotificationOccurrences.Next(schedule.Trigger, zone, occurrence);
        }

        schedule.NextRunAt = at?.UtcDateTime;
        schedule.UpdatedAt = now.UtcDateTime;
        return staged;
    }

    /// <summary>
    /// A timed campaign's occurrences: the audience is resolved once per scan, when the occurrence is
    /// near, so people who signed up or opted in since it was started are included.
    /// </summary>
    private async Task<int> StageCampaignAsync(NotificationSchedule schedule, DateTimeOffset now, DateTimeOffset horizon, CancellationToken cancellationToken)
    {
        var zone = StreakClock.Zone(schedule.Campaign!.TimeZoneId);
        CampaignAudience? audience = null;
        var staged = 0;
        var at = (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(schedule.NextRunAt!.Value, DateTimeKind.Utc));
        while (at is { } occurrence && occurrence <= horizon)
        {
            if (occurrence >= now - Grace)
            {
                audience ??= await _audiences.ResolveAsync(schedule.Audience, schedule.Message.Kind, schedule.Channels, null, cancellationToken);
                await _stager.StageAsync(schedule, occurrence, audience.Recipients, cancellationToken);
                schedule.LastRunAt = occurrence.UtcDateTime;
                staged++;
            }
            at = NotificationOccurrences.Next(schedule.Trigger, zone, occurrence);
        }

        schedule.NextRunAt = at?.UtcDateTime;
        schedule.UpdatedAt = now.UtcDateTime;
        return staged;
    }

    /// <summary>The idempotency key of one delivery: schedule, occurrence, recipient and channel.</summary>
    public static string DeliveryKey(Guid scheduleId, DateTimeOffset occurrence, Guid userId, string channel) =>
        $"schedule:{scheduleId:N}:{occurrence.UtcDateTime:yyyyMMddTHHmm}:{userId:N}:{channel}";

    private async Task<List<Recipient>> RecipientsAsync(NotificationSchedule schedule, CancellationToken cancellationToken)
    {
        // One person for now. A later audience (a segment, everyone) resolves here, to the same list.
        if (schedule.Audience.Kind != NotificationAudienceKind.User || schedule.Audience.UserId is not { } userId) return [];
        return await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId && !u.Banned)
            .Select(u => new Recipient(u.Id, u.TimeZoneId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The evening reminder's schedule, created on the first scan so an administrator can turn it off.</summary>
    private async Task EnsureBuiltInAsync(CancellationToken cancellationToken)
    {
        if (await _context.NotificationSchedules.AnyAsync(s => s.Key == EveningReminderKey, cancellationToken)) return;
        var now = _time.GetUtcNow().UtcDateTime;
        var schedule = new NotificationSchedule
        {
            Id = Guid.NewGuid(),
            Key = EveningReminderKey,
            Owner = NotificationScheduleOwner.Platform,
            Audience = NotificationAudience.OfEvent(),
            Message = new NotificationTemplate
            {
                Type = Planning.EveningReminders.NotificationType,
                Kind = NotificationKinds.Reminders,
                Title = Planning.EveningReminders.NotificationTitle,
            },
            Channels = NotificationChannels.All,
            Trigger = NotificationTrigger.AfterEvent(EatingWindowEndEvent, -(int)Planning.EveningReminderSchedule.Lead.TotalMinutes),
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.NotificationSchedules.Add(schedule);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another instance created it first: forget this copy, and let the scan read theirs.
            _context.NotificationSchedules.Remove(schedule);
            if (!await _context.NotificationSchedules.AsNoTracking().AnyAsync(s => s.Key == EveningReminderKey, cancellationToken)) throw;
        }
    }

    private sealed record Recipient(Guid Id, string? TimeZoneId);
}

/// <summary>
/// Creates schedules: the way an administrator's campaign or a person's own reminder is stored.
/// The first occurrence is worked out here, in the recipient's time zone, so the scheduler only reads
/// <see cref="NotificationSchedule.NextRunAt"/>.
/// </summary>
public sealed class NotificationSchedules
{
    private readonly IMizanDbContext _context;
    private readonly TimeProvider _time;

    public NotificationSchedules(IMizanDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    /// <summary>Stages a schedule for one person. The caller saves.</summary>
    public async Task<NotificationSchedule> CreateForPersonAsync(
        Guid userId,
        NotificationScheduleOwner owner,
        string type,
        NotificationMessage message,
        string? linkUrl,
        NotificationTrigger trigger,
        NotificationChannels channels = NotificationChannels.All,
        CancellationToken cancellationToken = default,
        ReminderDetails? reminder = null)
    {
        if (!NotificationKinds.IsKnown(message.Kind))
            throw new Exceptions.DomainValidationException($"\"{message.Kind}\" is not a notification kind.");
        var zoneId = await _context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.TimeZoneId).FirstOrDefaultAsync(cancellationToken);
        var now = _time.GetUtcNow();
        var schedule = new NotificationSchedule
        {
            Id = Guid.NewGuid(),
            Owner = owner,
            OwnerUserId = owner == NotificationScheduleOwner.User ? userId : null,
            Audience = NotificationAudience.Person(userId),
            Message = Template(type, message, linkUrl),
            Channels = channels,
            Trigger = trigger,
            Reminder = reminder,
            Enabled = true,
            NextRunAt = NotificationOccurrences.Next(trigger, StreakClock.Zone(zoneId), now)?.UtcDateTime,
            CreatedAt = now.UtcDateTime,
            UpdatedAt = now.UtcDateTime,
        };
        _context.NotificationSchedules.Add(schedule);
        return schedule;
    }

    /// <summary>The template a schedule sends, from a message and its link.</summary>
    public static NotificationTemplate Template(string type, NotificationMessage message, string? linkUrl) => new()
    {
        Type = type,
        Kind = message.Kind,
        Title = message.Title,
        Body = message.Body,
        ImageUrl = message.ImageUrl,
        LinkUrl = linkUrl,
        Actions = (message.Actions ?? []).Take(NotificationMessage.MaxActions).Select(a => new NotificationActionItem { Label = a.Label, Path = a.Path }).ToList(),
        Facts = (message.Facts ?? []).Select(f => new NotificationFactItem { Label = f.Label, Value = f.Value }).ToList(),
    };
}

/// <summary>Sends one queued delivery of a timed schedule, if the schedule is still enabled.</summary>
public sealed class NotificationDeliveries
{
    private readonly IMizanDbContext _context;
    private readonly INotificationWriter _writer;
    private readonly CampaignAudiences _audiences;
    private readonly IEnumerable<INotificationEventSource> _sources;
    private readonly IAppUrls _urls;

    public NotificationDeliveries(
        IMizanDbContext context,
        INotificationWriter writer,
        CampaignAudiences audiences,
        IEnumerable<INotificationEventSource> sources,
        IAppUrls urls)
    {
        _context = context;
        _writer = writer;
        _audiences = audiences;
        _sources = sources;
        _urls = urls;
    }

    public async Task<bool> SendAsync(NotificationDeliveryJob job, CancellationToken cancellationToken)
    {
        var schedule = await _context.NotificationSchedules.AsNoTracking().FirstOrDefaultAsync(s => s.Id == job.ScheduleId, cancellationToken);
        if (schedule is not { Enabled: true }) return false;
        // Skipped or done for today, a snooze included.
        if (schedule.SkipUntil is { } skip && job.Occurrence < skip) return false;
        var channel = NotificationKinds.Each.FirstOrDefault(c => NotificationKinds.Name(c) == job.Channel);
        if (channel == NotificationChannels.None) return false;

        var template = schedule.Message;
        var message = new NotificationMessage(
            template.Kind,
            template.Title,
            template.Body,
            template.ImageUrl,
            (template.Actions ?? []).Select(a => new NotificationAction(a.Label, a.Path)).ToList(),
            (template.Facts ?? []).Select(f => new NotificationFact(f.Label, f.Value)).ToList());

        if (schedule.Campaign is not null)
        {
            // Asked again when it goes out: a person who opted out, changed plan or came back since
            // the scan is left out, and an event that moved is not the one this was staged for.
            if (schedule.Campaign.Status != CampaignStatus.Active) return false;
            var audience = await _audiences.ResolveAsync(schedule.Audience, template.Kind, channel, [job.UserId], cancellationToken);
            if (audience.Recipients.Count == 0) return false;
            if (schedule.Trigger.Kind == NotificationTriggerKind.EventOffset
                && _sources.FirstOrDefault(s => s.Event == schedule.Trigger.Event) is { } source
                && !await source.HoldsAsync(schedule, job.UserId, job.Occurrence, cancellationToken))
                return false;
            message = CampaignMessages.Personalise(message, audience.Recipients[0].Name, _urls.Home());
        }

        var occurrence = new DateTimeOffset(DateTime.SpecifyKind(job.Occurrence, DateTimeKind.Utc));
        await _writer.SendAsync(
            new NotificationSend(job.UserId, template.Type, message, template.LinkUrl)
            {
                Channels = channel,
                DeliveryKey = NotificationScheduler.DeliveryKey(job.ScheduleId, occurrence, job.UserId, job.Channel),
                ReminderId = schedule.Reminder is null ? null : schedule.Id,
                ScheduleId = schedule.Campaign is null ? null : schedule.Id,
            },
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
