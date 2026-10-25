using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Planning;

/// <summary>The queued reminder: whose, which plan, which plan day, and the instant it was scheduled for.</summary>
public sealed record EveningReminderJob(Guid UserId, Guid PlanId, DateOnly Day, DateTime At);

/// <summary>
/// The evening reminder of an active plan with an eating window
/// (docs/PLANNING.md#telegram). A scan schedules each person's next reminder as
/// an outbox job keyed by person, day and time, so scanning again adds nothing
/// and a moved window gets a job of its own. When the job runs, the reminder is
/// checked again against the plan as it is now and sent as an in-app
/// notification (with push where a phone is registered) and to Telegram when
/// the account is linked.
/// </summary>
public sealed class EveningReminders
{
    public const string NotificationType = "evening_reminder";
    public const string NotificationTitle = "Eating window";

    private readonly IMizanDbContext _context;
    private readonly IOutbox _outbox;
    private readonly INotificationWriter _notifications;
    private readonly ITelegramMessenger _telegram;
    private readonly TimeProvider _time;

    public EveningReminders(
        IMizanDbContext context, IOutbox outbox, INotificationWriter notifications, ITelegramMessenger telegram, TimeProvider time)
    {
        _context = context;
        _outbox = outbox;
        _notifications = notifications;
        _telegram = telegram;
        _time = time;
    }

    /// <summary>
    /// Queues the reminder of everyone with an active plan that has an eating window whose next
    /// reminder falls within <see cref="EveningReminderSchedule.Lookahead"/>. Returns how many were
    /// found; one already queued is found again and not duplicated.
    /// </summary>
    public async Task<int> ScheduleAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var horizon = DateOnly.FromDateTime(now.UtcDateTime).AddDays(2);

        var plans = (await _context.MealPlans.AsNoTracking()
                .Where(p => p.Kind == PlanKind.Personal && p.Status == PlanStatus.Active
                    && p.UserId != null && p.StartDate != null && p.StartDate <= horizon)
                .Select(p => new PlanWindow(p.Id, p.UserId!.Value, p.HouseholdId, p.StartDate, p.DurationDays, p.Rules.EatingWindowEnd, p.UpdatedAt))
                .ToListAsync(cancellationToken))
            .Where(p => p.WindowEnd is not null)
            .ToList();
        if (plans.Count == 0) return 0;

        var userIds = plans.Select(p => p.UserId).Distinct().ToList();
        var zones = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.TimeZoneId })
            .ToDictionaryAsync(u => u.Id, u => u.TimeZoneId, cancellationToken);
        var lastSent = await LastSentAsync(userIds, cancellationToken);

        var staged = 0;
        foreach (var mine in plans.GroupBy(p => p.UserId))
        {
            var zone = StreakClock.Zone(zones.GetValueOrDefault(mine.Key));
            var sent = lastSent.GetValueOrDefault(mine.Key);

            // The order GetPlanDayQuery picks a day's plan in: their own plan first, then the newest.
            foreach (var plan in mine.OrderBy(p => p.HouseholdId == null ? 0 : 1).ThenByDescending(p => p.UpdatedAt))
            {
                if (EveningReminderSchedule.Next(plan.WindowEnd, plan.StartDate, plan.DurationDays, zone, now, sent) is not { } due) continue;
                if (due.At > now + EveningReminderSchedule.Lookahead) break;

                var at = due.At.UtcDateTime;
                await _outbox.ScheduleAsync(
                    OutboxJobTypes.EveningReminder,
                    new EveningReminderJob(plan.UserId, plan.Id, due.Day, at),
                    runAfter: at,
                    dedupeKey: $"evening-reminder:{plan.UserId:N}:{due.Day:yyyyMMdd}:{at:HHmm}",
                    cancellationToken);
                staged++;
                break;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return staged;
    }

    /// <summary>Sends one queued reminder if it is still due. Returns whether it was sent.</summary>
    public async Task<bool> SendAsync(EveningReminderJob job, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var plan = await _context.MealPlans.AsNoTracking()
            .Where(p => p.Id == job.PlanId && p.UserId == job.UserId && p.Kind == PlanKind.Personal && p.Status == PlanStatus.Active)
            .Select(p => new { p.Id, p.StartDate, p.DurationDays, p.Rules.EatingWindowEnd, p.Rules.TargetCalories })
            .FirstOrDefaultAsync(cancellationToken);
        if (plan?.EatingWindowEnd is not { } windowEnd) return false;

        var zoneId = await _context.Users.AsNoTracking().Where(u => u.Id == job.UserId).Select(u => u.TimeZoneId).FirstOrDefaultAsync(cancellationToken);
        var lastSent = (await LastSentAsync([job.UserId], cancellationToken)).GetValueOrDefault(job.UserId);
        var next = EveningReminderSchedule.Next(windowEnd, plan.StartDate, plan.DurationDays, StreakClock.Zone(zoneId), now, lastSent);
        var scheduledAt = new DateTimeOffset(DateTime.SpecifyKind(job.At, DateTimeKind.Utc));
        if (!EveningReminderSchedule.IsDue(next, job.Day, scheduledAt, now)) return false;

        var calories = await _context.FoodDiaryEntries.AsNoTracking()
            .Where(e => e.UserId == job.UserId && e.EntryDate == job.Day)
            .SumAsync(e => e.Calories ?? 0m, cancellationToken);
        var text = EveningReminderText.Build(windowEnd, calories, plan.TargetCalories);

        await _notifications.AddAsync(job.UserId, NotificationType, NotificationTitle, text, $"/meal-plan/{plan.Id}", cancellationToken);
        await _telegram.QueueAsync(
            job.UserId,
            text,
            $"plan?planId={plan.Id:D}&day={job.Day:yyyy-MM-dd}",
            dedupeKey: $"telegram:evening-reminder:{job.UserId:N}:{job.Day:yyyyMMdd}",
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>When each person was last reminded, from the notifications themselves: the once-a-day rule needs no column of its own.</summary>
    private async Task<Dictionary<Guid, DateTimeOffset?>> LastSentAsync(List<Guid> userIds, CancellationToken cancellationToken) =>
        (await _context.Notifications.AsNoTracking()
            .Where(n => n.Type == NotificationType && userIds.Contains(n.UserId))
            .GroupBy(n => n.UserId)
            .Select(g => new { UserId = g.Key, At = g.Max(n => n.CreatedAt) })
            .ToListAsync(cancellationToken))
        .ToDictionary(x => x.UserId, x => (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(x.At, DateTimeKind.Utc)));

    private sealed record PlanWindow(Guid Id, Guid UserId, Guid? HouseholdId, DateOnly? StartDate, int DurationDays, TimeOnly? WindowEnd, DateTime UpdatedAt);
}
