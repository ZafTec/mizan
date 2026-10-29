using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Planning;
using Mizan.Contracts.Notifications;
using Mizan.Domain.Constants;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Notifications;

/// <summary>
/// A person's reminders (docs/ARCHITECTURE.md#reminders): their own notification schedules, with a
/// kind and what each is tied to. The web, MCP, the Telegram bot and the assistant all go through
/// these requests, so the rules in <see cref="ReminderRules"/> hold whichever one is used.
/// </summary>
public record CreateReminderCommand(CreateReminderRequest Request) : IRequest<ReminderDto>;

public record UpdateReminderCommand(Guid Id, UpdateReminderRequest Request) : IRequest<ReminderDto>;

public record DeleteReminderCommand(Guid Id) : IRequest;

public record ListRemindersQuery : IRequest<ReminderListResult>;

/// <summary>Sends the reminder again after a while, 30 minutes by default, on its channels.</summary>
public record SnoozeReminderCommand(Guid Id, int? Minutes = null) : IRequest<ReminderActionResult>;

/// <summary>Drops the rest of today's deliveries of the reminder, a snooze included.</summary>
public record SkipReminderCommand(Guid Id) : IRequest<ReminderActionResult>;

/// <summary>Done: like skipping the rest of today, and a one-off reminder is finished.</summary>
public record CompleteReminderCommand(Guid Id) : IRequest<ReminderActionResult>;

/// <summary>What the reminder handlers share: who is asking, their clock, their plan's cap, and the reminder as a DTO.</summary>
public sealed class ReminderBook
{
    public const string NotificationType = "reminder";

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEntitlementService _entitlements;
    private readonly TimeProvider _time;

    public ReminderBook(IMizanDbContext context, ICurrentUserService currentUser, IEntitlementService entitlements, TimeProvider time)
    {
        _context = context;
        _currentUser = currentUser;
        _entitlements = entitlements;
        _time = time;
    }

    public DateTimeOffset Now => _time.GetUtcNow();

    public Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    public async Task<TimeZoneInfo> ZoneAsync(CancellationToken cancellationToken) =>
        StreakClock.Zone(await _context.Users.AsNoTracking().Where(u => u.Id == UserId).Select(u => u.TimeZoneId).FirstOrDefaultAsync(cancellationToken));

    /// <summary>The caller's reminder, tracked. Anyone else's, or another kind of schedule, is "not found".</summary>
    public async Task<NotificationSchedule> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        return await _context.NotificationSchedules
            .FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == userId && s.Owner == NotificationScheduleOwner.User && s.Reminder != null, cancellationToken)
            ?? throw new EntityNotFoundException("Reminder not found.");
    }

    public static bool IsActive(NotificationSchedule schedule) => schedule.Enabled && schedule.NextRunAt != null;

    public async Task<int> ActiveCountAsync(Guid? except, CancellationToken cancellationToken)
    {
        var userId = UserId;
        return await _context.NotificationSchedules.CountAsync(
            s => s.OwnerUserId == userId && s.Owner == NotificationScheduleOwner.User && s.Reminder != null
                 && s.Enabled && s.NextRunAt != null && s.Id != except,
            cancellationToken);
    }

    public async Task<(int Limit, bool IsPro)> LimitAsync(CancellationToken cancellationToken)
    {
        var isPro = (await _entitlements.GetAsync(UserId, cancellationToken)).IsPro;
        return (ReminderRules.ActiveLimit(isPro), isPro);
    }

    /// <summary>Refuses one more active reminder past the plan's cap: an upgrade on Free, a plain refusal on Pro.</summary>
    public async Task EnsureRoomAsync(Guid? except, CancellationToken cancellationToken)
    {
        var (limit, isPro) = await LimitAsync(cancellationToken);
        if (await ActiveCountAsync(except, cancellationToken) < limit) return;
        if (!isPro)
            throw new UpgradeRequiredException($"Free accounts can keep {limit} reminders on. Turn one off, or upgrade to Pro for up to {ReminderRules.ProActiveLimit}.");
        throw new DomainValidationException($"You can keep {limit} reminders on. Turn one off first.");
    }

    public static NotificationChannels Channels(IReadOnlyList<string>? names)
    {
        if (names is null) return NotificationChannels.All;
        var channels = NotificationChannels.None;
        foreach (var name in names)
        {
            var channel = NotificationKinds.Each.FirstOrDefault(c => string.Equals(NotificationKinds.Name(c), name?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (channel == NotificationChannels.None)
                throw new DomainValidationException($"\"{name}\" is not a channel. Use inApp, telegram, push or email.");
            channels |= channel;
        }
        if (channels == NotificationChannels.None) throw new DomainValidationException("Choose at least one channel.");
        return channels;
    }

    public static string? CleanTitle(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > ReminderRules.MaxTitleLength) throw new DomainValidationException($"Keep the title under {ReminderRules.MaxTitleLength} characters.");
        return trimmed;
    }

    public static string? CleanNote(string? note)
    {
        var trimmed = note?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > ReminderRules.MaxNoteLength) throw new DomainValidationException($"Keep the note under {ReminderRules.MaxNoteLength} characters.");
        return trimmed;
    }

    public static NotificationTrigger Trigger(ReminderSchedule? schedule, TimeZoneInfo zone, DateTimeOffset now)
    {
        if (schedule is null) throw new DomainValidationException("Say when: a schedule with repeat and time.");
        var result = ReminderRules.Trigger(
            new ReminderTiming(schedule.Repeat, schedule.Time, schedule.Days, schedule.Date, schedule.EveryMinutes, schedule.EveryHours, schedule.EveryDays),
            zone,
            now);
        return result.Trigger ?? throw new DomainValidationException(result.Error!);
    }

    /// <summary>A pre-cook reminder's trigger, read from the plan's prep session.</summary>
    public static NotificationTrigger PreCookTrigger(MealPlan plan, PrepSession session, TimeOnly prepTime, int leadMinutes, TimeZoneInfo zone, DateTimeOffset now)
    {
        if (plan.StartDate is not { } start) throw new DomainValidationException("The plan has no start date, so its prep days have no date yet.");
        var result = ReminderRules.PreCook(start.AddDays(session.DayIndex), prepTime, leadMinutes, zone, now);
        return result.Trigger ?? throw new DomainValidationException(result.Error!);
    }

    /// <summary>The message a reminder sends: its title, its note, and one button to where the reminder is done.</summary>
    public static (NotificationMessage Message, string Path) Message(string title, string? note, ReminderDetails details, DateOnly? planDay)
    {
        var (label, path) = ReminderKinds.Action(details.Kind, details.MealPlanId, planDay);
        return (new NotificationMessage(NotificationKinds.Reminders, title, note, null, [new NotificationAction(label, path)]), path);
    }

    public static ReminderDto ToDto(NotificationSchedule schedule, TimeZoneInfo zone)
    {
        var details = schedule.Reminder ?? new ReminderDetails();
        var trigger = schedule.Trigger;
        string? time = trigger.LocalTime is { } t ? ReminderRules.FormatTime(t) : null;
        string? date = null;
        if (trigger.Kind is NotificationTriggerKind.Once or NotificationTriggerKind.Interval && trigger.At is { } at)
        {
            var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)), zone);
            date = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            time ??= trigger.Kind == NotificationTriggerKind.Once ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : null;
        }

        var action = schedule.Message.Actions.FirstOrDefault();
        return new ReminderDto(
            schedule.Id,
            details.Kind,
            ReminderKinds.Label(details.Kind),
            schedule.Message.Title,
            schedule.Message.Body,
            ReminderRules.RepeatOf(trigger),
            time,
            ReminderRules.DayNamesOf(trigger),
            date,
            trigger.Kind == NotificationTriggerKind.Interval ? trigger.IntervalMinutes : null,
            ReminderRules.Describe(trigger, zone),
            NotificationKinds.Each.Where(c => schedule.Channels.HasFlag(c)).Select(NotificationKinds.Name).ToList(),
            schedule.Enabled,
            IsActive(schedule),
            schedule.NextRunAt,
            schedule.Enabled && schedule.NextRunAt is { } next ? When(next, zone) : null,
            schedule.SkipUntil,
            details.WorkoutTemplateId,
            details.MealPlanId,
            details.PrepSessionId,
            details.PrepTime is { } prep ? ReminderRules.FormatTime(prep) : null,
            details.LeadMinutes,
            details.MealType,
            action?.Label ?? "Open Mizan",
            action?.Path ?? schedule.Message.LinkUrl ?? "/today",
            schedule.CreatedAt);
    }

    /// <summary>An instant on the person's clock, as every surface shows it: "Mon 2 Nov at 07:00".</summary>
    public static string When(DateTime utc, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), zone);
        return $"{local.ToString("ddd d MMM", CultureInfo.InvariantCulture)} at {local.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    }

    /// <summary>The next firing from now, worked out again after a change, so a stale time is never kept.</summary>
    public void Reschedule(NotificationSchedule schedule, TimeZoneInfo zone)
    {
        var now = Now;
        schedule.NextRunAt = schedule.Enabled ? NotificationOccurrences.Next(schedule.Trigger, zone, now)?.UtcDateTime : null;
        schedule.UpdatedAt = now.UtcDateTime;
    }
}

public sealed class CreateReminderCommandHandler : IRequestHandler<CreateReminderCommand, ReminderDto>
{
    private readonly IMizanDbContext _context;
    private readonly ReminderBook _book;
    private readonly NotificationSchedules _schedules;
    private readonly PlanAccess _plans;

    public CreateReminderCommandHandler(IMizanDbContext context, ReminderBook book, NotificationSchedules schedules, PlanAccess plans)
    {
        _context = context;
        _book = book;
        _schedules = schedules;
        _plans = plans;
    }

    public async Task<ReminderDto> Handle(CreateReminderCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var userId = _book.UserId;
        var kind = request.Kind?.Trim().ToLowerInvariant();
        if (!ReminderKinds.IsKnown(kind))
            throw new DomainValidationException($"\"{request.Kind}\" is not a reminder kind. Use {string.Join(", ", ReminderKinds.All)}.");

        var zone = await _book.ZoneAsync(cancellationToken);
        var now = _book.Now;
        var title = ReminderBook.CleanTitle(request.Title);
        var note = ReminderBook.CleanNote(request.Note);
        var channels = ReminderBook.Channels(request.Channels);
        var details = new ReminderDetails { Kind = kind! };
        DateOnly? planDay = null;
        NotificationTrigger trigger;

        switch (kind)
        {
            case ReminderKinds.PreCook:
            {
                if (request.MealPlanId is not { } planId || request.PrepSessionId is not { } sessionId)
                    throw new DomainValidationException("A pre-cook reminder needs the plan and its prep session.");
                var plan = await _plans.LoadReadableAsync(planId, cancellationToken);
                var session = plan.PrepSessions.FirstOrDefault(s => s.Id == sessionId)
                    ?? throw new EntityNotFoundException("Prep session not found in that plan.");
                var prepTime = ParseTime(request.PrepTime) ?? ReminderRules.DefaultPrepTime;
                var lead = request.LeadMinutes ?? ReminderRules.DefaultLeadMinutes;
                trigger = ReminderBook.PreCookTrigger(plan, session, prepTime, lead, zone, now);
                planDay = plan.StartDate!.Value.AddDays(session.DayIndex);
                details.MealPlanId = plan.Id;
                details.PrepSessionId = session.Id;
                details.PrepTime = prepTime;
                details.LeadMinutes = lead;
                title ??= ReminderKinds.DefaultTitle(kind, session.Title);
                break;
            }

            case ReminderKinds.Workout:
            {
                trigger = ReminderBook.Trigger(request.Schedule, zone, now);
                string? templateName = null;
                if (request.WorkoutTemplateId is { } templateId)
                {
                    templateName = await _context.WorkoutTemplates.AsNoTracking()
                        .Where(t => t.Id == templateId && (t.UserId == userId || t.IsBuiltIn))
                        .Select(t => t.Name)
                        .FirstOrDefaultAsync(cancellationToken)
                        ?? throw new EntityNotFoundException("Workout template not found.");
                    details.WorkoutTemplateId = templateId;
                }
                title ??= ReminderKinds.DefaultTitle(kind, templateName);
                break;
            }

            case ReminderKinds.MealLog:
            {
                trigger = ReminderBook.Trigger(request.Schedule, zone, now);
                if (!string.IsNullOrWhiteSpace(request.MealType))
                {
                    if (!MealTypes.IsValid(request.MealType)) throw new DomainValidationException($"\"{request.MealType}\" is not a meal. Use BREAKFAST, LUNCH, DINNER or SNACK.");
                    details.MealType = request.MealType.Trim().ToUpperInvariant();
                }
                title ??= ReminderKinds.DefaultTitle(kind, details.MealType);
                break;
            }

            case ReminderKinds.Custom:
                trigger = ReminderBook.Trigger(request.Schedule, zone, now);
                if (title is null) throw new DomainValidationException("Say what to remind you about: a custom reminder needs a title.");
                break;

            default:
                trigger = ReminderBook.Trigger(request.Schedule, zone, now);
                title ??= ReminderKinds.DefaultTitle(kind!);
                break;
        }

        await _book.EnsureRoomAsync(null, cancellationToken);

        var (message, path) = ReminderBook.Message(title, note, details, planDay);
        var schedule = await _schedules.CreateForPersonAsync(
            userId,
            NotificationScheduleOwner.User,
            ReminderBook.NotificationType,
            message,
            path,
            trigger,
            channels,
            cancellationToken,
            details);
        await _context.SaveChangesAsync(cancellationToken);
        return ReminderBook.ToDto(schedule, zone);
    }

    internal static TimeOnly? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return ReminderRules.TryParseTime(value, out var time) ? time : throw new DomainValidationException("Give the time as HH:mm, such as 18:00.");
    }
}

public sealed class UpdateReminderCommandHandler : IRequestHandler<UpdateReminderCommand, ReminderDto>
{
    private readonly IMizanDbContext _context;
    private readonly ReminderBook _book;
    private readonly PlanAccess _plans;

    public UpdateReminderCommandHandler(IMizanDbContext context, ReminderBook book, PlanAccess plans)
    {
        _context = context;
        _book = book;
        _plans = plans;
    }

    public async Task<ReminderDto> Handle(UpdateReminderCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var schedule = await _book.LoadAsync(command.Id, cancellationToken);
        var details = schedule.Reminder!;
        var zone = await _book.ZoneAsync(cancellationToken);
        var now = _book.Now;
        var wasActive = ReminderBook.IsActive(schedule);
        DateOnly? planDay = null;

        if (details.Kind == ReminderKinds.PreCook)
        {
            if (request.Schedule is not null) throw new DomainValidationException("A pre-cook reminder follows its prep session: change the prep time or how long before instead.");
            var plan = await _plans.LoadReadableAsync(details.MealPlanId!.Value, cancellationToken);
            var session = plan.PrepSessions.FirstOrDefault(s => s.Id == details.PrepSessionId)
                ?? throw new EntityNotFoundException("That prep session is no longer in the plan.");
            planDay = plan.StartDate?.AddDays(session.DayIndex);
            if (request.PrepTime is not null || request.LeadMinutes is not null)
            {
                details.PrepTime = CreateReminderCommandHandler.ParseTime(request.PrepTime) ?? details.PrepTime ?? ReminderRules.DefaultPrepTime;
                details.LeadMinutes = request.LeadMinutes ?? details.LeadMinutes ?? ReminderRules.DefaultLeadMinutes;
                schedule.Trigger = ReminderBook.PreCookTrigger(plan, session, details.PrepTime.Value, details.LeadMinutes.Value, zone, now);
            }
        }
        else if (request.Schedule is not null)
        {
            schedule.Trigger = ReminderBook.Trigger(request.Schedule, zone, now);
        }

        if (request.Channels is not null) schedule.Channels = ReminderBook.Channels(request.Channels);
        if (request.Enabled is { } enabled) schedule.Enabled = enabled;

        var title = request.Title is null ? schedule.Message.Title : ReminderBook.CleanTitle(request.Title)
            ?? throw new DomainValidationException("A reminder needs a title.");
        var note = request.Note is null ? schedule.Message.Body : ReminderBook.CleanNote(request.Note);
        var (message, path) = ReminderBook.Message(title, note, details, planDay);
        schedule.Message = NotificationSchedules.Template(ReminderBook.NotificationType, message, path);
        // A changed owned value has to be reassigned for the JSON column to be written.
        schedule.Reminder = new ReminderDetails
        {
            Kind = details.Kind,
            WorkoutTemplateId = details.WorkoutTemplateId,
            MealPlanId = details.MealPlanId,
            PrepSessionId = details.PrepSessionId,
            PrepTime = details.PrepTime,
            LeadMinutes = details.LeadMinutes,
            MealType = details.MealType,
        };

        _book.Reschedule(schedule, zone);
        if (ReminderBook.IsActive(schedule) && !wasActive) await _book.EnsureRoomAsync(schedule.Id, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return ReminderBook.ToDto(schedule, zone);
    }
}

public sealed class DeleteReminderCommandHandler : IRequestHandler<DeleteReminderCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ReminderBook _book;

    public DeleteReminderCommandHandler(IMizanDbContext context, ReminderBook book)
    {
        _context = context;
        _book = book;
    }

    /// <summary>Deliveries already queued find the schedule gone and send nothing.</summary>
    public async Task Handle(DeleteReminderCommand command, CancellationToken cancellationToken)
    {
        var schedule = await _book.LoadAsync(command.Id, cancellationToken);
        _context.NotificationSchedules.Remove(schedule);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ListRemindersQueryHandler : IRequestHandler<ListRemindersQuery, ReminderListResult>
{
    private readonly IMizanDbContext _context;
    private readonly ReminderBook _book;

    public ListRemindersQueryHandler(IMizanDbContext context, ReminderBook book)
    {
        _context = context;
        _book = book;
    }

    public async Task<ReminderListResult> Handle(ListRemindersQuery request, CancellationToken cancellationToken)
    {
        var userId = _book.UserId;
        var zone = await _book.ZoneAsync(cancellationToken);
        var rows = await _context.NotificationSchedules.AsNoTracking()
            .Where(s => s.OwnerUserId == userId && s.Owner == NotificationScheduleOwner.User && s.Reminder != null)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
        var (limit, isPro) = await _book.LimitAsync(cancellationToken);
        return new ReminderListResult(
            rows.Select(s => ReminderBook.ToDto(s, zone)).ToList(),
            rows.Count(ReminderBook.IsActive),
            limit,
            isPro);
    }
}

public sealed class SnoozeReminderCommandHandler : IRequestHandler<SnoozeReminderCommand, ReminderActionResult>
{
    private readonly IMizanDbContext _context;
    private readonly ReminderBook _book;
    private readonly IOutbox _outbox;

    public SnoozeReminderCommandHandler(IMizanDbContext context, ReminderBook book, IOutbox outbox)
    {
        _context = context;
        _book = book;
        _outbox = outbox;
    }

    /// <summary>
    /// Queues the reminder again on its channels, keyed like any occurrence of the schedule, so a
    /// second press in the same minute adds nothing. A skip for today no longer holds it back.
    /// </summary>
    public async Task<ReminderActionResult> Handle(SnoozeReminderCommand command, CancellationToken cancellationToken)
    {
        var minutes = command.Minutes ?? ReminderRules.DefaultSnoozeMinutes;
        if (ReminderRules.CheckSnooze(minutes) is { } error) throw new DomainValidationException(error);

        var schedule = await _book.LoadAsync(command.Id, cancellationToken);
        if (!schedule.Enabled) throw new DomainValidationException("That reminder is off. Turn it on to snooze it.");
        var zone = await _book.ZoneAsync(cancellationToken);
        var now = _book.Now;
        var at = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).AddMinutes(minutes);
        var userId = _book.UserId;

        foreach (var channel in NotificationKinds.Each.Where(c => schedule.Channels.HasFlag(c)))
        {
            var name = NotificationKinds.Name(channel);
            await _outbox.ScheduleAsync(
                OutboxJobTypes.NotificationDelivery,
                new NotificationDeliveryJob(schedule.Id, userId, at.UtcDateTime, name),
                at.UtcDateTime,
                NotificationScheduler.DeliveryKey(schedule.Id, at, userId, name),
                cancellationToken);
        }

        schedule.SkipUntil = null;
        schedule.UpdatedAt = now.UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);

        var local = TimeZoneInfo.ConvertTime(at, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
        return new ReminderActionResult(ReminderBook.ToDto(schedule, zone), $"Snoozed. I will remind you again at {local}.", at.UtcDateTime);
    }
}

public sealed class SkipReminderCommandHandler : IRequestHandler<SkipReminderCommand, ReminderActionResult>
{
    private readonly IMizanDbContext _context;
    private readonly ReminderBook _book;

    public SkipReminderCommandHandler(IMizanDbContext context, ReminderBook book)
    {
        _context = context;
        _book = book;
    }

    public async Task<ReminderActionResult> Handle(SkipReminderCommand command, CancellationToken cancellationToken)
    {
        var schedule = await _book.LoadAsync(command.Id, cancellationToken);
        var zone = await _book.ZoneAsync(cancellationToken);
        var now = _book.Now;
        schedule.SkipUntil = ReminderRules.EndOfToday(now, zone).UtcDateTime;
        schedule.UpdatedAt = now.UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);

        var dto = ReminderBook.ToDto(schedule, zone);
        return new ReminderActionResult(dto, Next("Skipped for today.", schedule, zone));
    }

    internal static string Next(string lead, NotificationSchedule schedule, TimeZoneInfo zone)
    {
        // The next firing past the skip, which may still be today's later occurrences in the scheduler's eyes.
        var after = schedule.SkipUntil is { } skip ? new DateTimeOffset(DateTime.SpecifyKind(skip, DateTimeKind.Utc)).AddTicks(-1) : (DateTimeOffset?)null;
        var next = after is { } from ? NotificationOccurrences.Next(schedule.Trigger, zone, from) : null;
        return schedule.Enabled && next is { } at ? $"{lead} Next: {ReminderBook.When(at.UtcDateTime, zone)}." : lead;
    }
}

public sealed class CompleteReminderCommandHandler : IRequestHandler<CompleteReminderCommand, ReminderActionResult>
{
    private readonly IMizanDbContext _context;
    private readonly ReminderBook _book;

    public CompleteReminderCommandHandler(IMizanDbContext context, ReminderBook book)
    {
        _context = context;
        _book = book;
    }

    public async Task<ReminderActionResult> Handle(CompleteReminderCommand command, CancellationToken cancellationToken)
    {
        var schedule = await _book.LoadAsync(command.Id, cancellationToken);
        var zone = await _book.ZoneAsync(cancellationToken);
        var now = _book.Now;
        schedule.SkipUntil = ReminderRules.EndOfToday(now, zone).UtcDateTime;
        if (schedule.Trigger.Kind == NotificationTriggerKind.Once) schedule.Enabled = false;
        schedule.UpdatedAt = now.UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);

        return new ReminderActionResult(ReminderBook.ToDto(schedule, zone), SkipReminderCommandHandler.Next("Done.", schedule, zone));
    }
}
