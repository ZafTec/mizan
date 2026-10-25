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
/// (docs/PLANNING.md#telegram). Everyone who follows the plan is reminded: its
/// owner and, for a household plan, each member, each in their own time zone and
/// with their own diary, unless their own personal plan is the one they follow
/// that day. A scan schedules each person's next reminder as an outbox job keyed
/// by person, day and time, so scanning again adds nothing and a moved window
/// gets a job of its own. When the job runs, the reminder is checked again
/// against the plans as they are now and sent as an in-app notification, which
/// carries it to push and, when the person has opted in, to Telegram.
/// </summary>
public sealed class EveningReminders
{
    public const string NotificationType = "evening_reminder";
    public const string NotificationTitle = "Eating window";

    private readonly IMizanDbContext _context;
    private readonly IOutbox _outbox;
    private readonly INotificationWriter _notifications;
    private readonly TimeProvider _time;

    public EveningReminders(
        IMizanDbContext context, IOutbox outbox, INotificationWriter notifications, TimeProvider time)
    {
        _context = context;
        _outbox = outbox;
        _notifications = notifications;
        _time = time;
    }

    /// <summary>
    /// Queues the reminder of everyone following an active plan that has an eating window whose
    /// next reminder falls within <see cref="EveningReminderSchedule.Lookahead"/>. Returns how many
    /// were found; one already queued is found again and not duplicated.
    /// </summary>
    public async Task<int> ScheduleAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var horizon = DateOnly.FromDateTime(now.UtcDateTime).AddDays(2);

        // Every active plan, with a window or without: a plan without one still decides
        // that its follower is not reminded about another plan's window that day.
        var plans = await ActivePlans()
            .Where(p => p.StartDate <= horizon)
            .Select(Window)
            .ToListAsync(cancellationToken);
        if (!plans.Any(p => p.WindowEnd is not null)) return 0;

        var households = plans.Where(p => p.HouseholdId != null).Select(p => p.HouseholdId!.Value).Distinct().ToList();
        var members = households.Count == 0
            ? []
            : await _context.HouseholdMembers.AsNoTracking()
                .Where(m => households.Contains(m.HouseholdId))
                .Select(m => new Membership(m.HouseholdId, m.UserId))
                .ToListAsync(cancellationToken);

        var followers = Followers(plans, members);
        var userIds = followers.Where(f => f.Value.Any(p => p.WindowEnd is not null)).Select(f => f.Key).ToList();
        if (userIds.Count == 0) return 0;

        var zones = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.TimeZoneId })
            .ToDictionaryAsync(u => u.Id, u => u.TimeZoneId, cancellationToken);
        var lastSent = await LastSentAsync(userIds, cancellationToken);

        var staged = 0;
        foreach (var userId in userIds)
        {
            var zone = StreakClock.Zone(zones.GetValueOrDefault(userId));
            if (EveningReminderSchedule.NextFor(Ordered(followers[userId], userId), zone, now, lastSent.GetValueOrDefault(userId)) is not { } next) continue;
            if (next.Due.At > now + EveningReminderSchedule.Lookahead) continue;

            var at = next.Due.At.UtcDateTime;
            await _outbox.ScheduleAsync(
                OutboxJobTypes.EveningReminder,
                new EveningReminderJob(userId, next.Plan.PlanId, next.Due.Day, at),
                runAfter: at,
                dedupeKey: $"evening-reminder:{userId:N}:{next.Due.Day:yyyyMMdd}:{at:HHmm}",
                cancellationToken);
            staged++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return staged;
    }

    /// <summary>
    /// Sends one queued reminder if it is still due. The reminder is for the plan the person
    /// follows that day when it is sent, which is the queued plan unless the person's plans
    /// changed and the new one closes its window at the same time. Returns whether it was sent.
    /// </summary>
    public async Task<bool> SendAsync(EveningReminderJob job, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var households = await _context.HouseholdMembers.AsNoTracking()
            .Where(m => m.UserId == job.UserId)
            .Select(m => m.HouseholdId)
            .ToListAsync(cancellationToken);
        var plans = await ActivePlans()
            .Where(p => p.UserId == job.UserId || (p.HouseholdId != null && households.Contains(p.HouseholdId.Value)))
            .Select(Window)
            .ToListAsync(cancellationToken);

        var zoneId = await _context.Users.AsNoTracking().Where(u => u.Id == job.UserId).Select(u => u.TimeZoneId).FirstOrDefaultAsync(cancellationToken);
        var lastSent = (await LastSentAsync([job.UserId], cancellationToken)).GetValueOrDefault(job.UserId);
        var next = EveningReminderSchedule.NextFor(Ordered(plans, job.UserId), StreakClock.Zone(zoneId), now, lastSent);
        var scheduledAt = new DateTimeOffset(DateTime.SpecifyKind(job.At, DateTimeKind.Utc));
        if (next is not { } followed || !EveningReminderSchedule.IsDue(followed.Due, job.Day, scheduledAt, now)) return false;
        var plan = followed.Plan;

        var calories = await _context.FoodDiaryEntries.AsNoTracking()
            .Where(e => e.UserId == job.UserId && e.EntryDate == job.Day)
            .SumAsync(e => e.Calories ?? 0m, cancellationToken);
        var text = EveningReminderText.Build(plan.WindowEnd!.Value, calories, plan.TargetCalories);

        // The day is named so a reminder sent the evening before opens the right day.
        await _notifications.AddAsync(job.UserId, NotificationType, NotificationTitle, text, $"/meal-plan/{plan.PlanId}?day={job.Day:yyyy-MM-dd}", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<MealPlan> ActivePlans() =>
        _context.MealPlans.AsNoTracking()
            .Where(p => p.Kind == PlanKind.Personal && p.Status == PlanStatus.Active && p.StartDate != null);

    private static readonly System.Linq.Expressions.Expression<Func<MealPlan, PlanWindow>> Window = p =>
        new PlanWindow(p.Id, p.UserId, p.HouseholdId, p.StartDate, p.DurationDays, p.Rules.EatingWindowEnd, p.Rules.TargetCalories, p.UpdatedAt);

    /// <summary>Who follows each plan: its owner and, for a household plan, every member of the household.</summary>
    private static Dictionary<Guid, List<PlanWindow>> Followers(List<PlanWindow> plans, List<Membership> members)
    {
        var byHousehold = members.ToLookup(m => m.HouseholdId, m => m.UserId);
        var followers = new Dictionary<Guid, List<PlanWindow>>();
        foreach (var plan in plans)
        {
            var people = plan.HouseholdId is { } household ? byHousehold[household] : [];
            if (plan.UserId is { } owner) people = people.Append(owner);
            foreach (var person in people.Distinct())
            {
                if (!followers.TryGetValue(person, out var list)) followers[person] = list = [];
                list.Add(plan);
            }
        }
        return followers;
    }

    /// <summary>
    /// A person's plans in the order <see cref="GetPlanDayQuery"/> picks a day's plan in: their own
    /// personal plan, their own household plan, another member's household plan, each newest first.
    /// </summary>
    private static List<FollowedPlan> Ordered(IEnumerable<PlanWindow> plans, Guid userId) =>
        plans
            .OrderBy(p => p.UserId == userId && p.HouseholdId == null ? 0 : p.UserId == userId ? 1 : 2)
            .ThenByDescending(p => p.UpdatedAt)
            .Select(p => new FollowedPlan(p.Id, p.StartDate, p.DurationDays, p.WindowEnd, p.TargetCalories))
            .ToList();

    /// <summary>When each person was last reminded, from the notifications themselves: the once-a-day rule needs no column of its own.</summary>
    private async Task<Dictionary<Guid, DateTimeOffset?>> LastSentAsync(List<Guid> userIds, CancellationToken cancellationToken) =>
        (await _context.Notifications.AsNoTracking()
            .Where(n => n.Type == NotificationType && userIds.Contains(n.UserId))
            .GroupBy(n => n.UserId)
            .Select(g => new { UserId = g.Key, At = g.Max(n => n.CreatedAt) })
            .ToListAsync(cancellationToken))
        .ToDictionary(x => x.UserId, x => (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(x.At, DateTimeKind.Utc)));

    private sealed record PlanWindow(
        Guid Id, Guid? UserId, Guid? HouseholdId, DateOnly? StartDate, int DurationDays, TimeOnly? WindowEnd, decimal? TargetCalories, DateTime UpdatedAt);

    private sealed record Membership(Guid HouseholdId, Guid UserId);
}
