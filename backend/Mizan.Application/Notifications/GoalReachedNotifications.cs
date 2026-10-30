using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Goals;

namespace Mizan.Application.Notifications;

/// <summary>
/// Tells a person when a body measurement reaches a target of their active goal
/// (<see cref="GoalReached"/>). It is an achievements notification, sent to each channel the
/// person allows for that kind, once per goal and target: a later measurement that crosses the
/// same target again sends nothing.
/// </summary>
public sealed class GoalReachedNotifications
{
    public const string NotificationType = "goal_reached";

    private readonly IMizanDbContext _context;
    private readonly INotificationWriter _notifications;

    public GoalReachedNotifications(IMizanDbContext context, INotificationWriter notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    /// <summary>Checks a measurement the caller has saved, stages what it reached and saves.</summary>
    public async Task NotifyAsync(BodyMeasurement measurement, CancellationToken cancellationToken)
    {
        var goal = await _context.UserGoals.AsNoTracking()
            .Where(g => g.UserId == measurement.UserId && g.IsActive)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (goal is null) return;
        var targets = new BodyTargets(GoalReached.TargetWeightKg(goal.TargetWeight, goal.WeightUnit), goal.TargetBodyFatPercentage, goal.TargetMuscleMassKg);
        if (targets is { WeightKg: null, BodyFatPercentage: null, MuscleMassKg: null }) return;

        // A back-filled measurement older than the latest one does not reach anything.
        var others = _context.BodyMeasurements.AsNoTracking()
            .Where(m => m.UserId == measurement.UserId && m.Id != measurement.Id);
        if (await others.AnyAsync(m => m.MeasurementDate > measurement.MeasurementDate, cancellationToken)) return;

        var earlier = others.Where(m => m.MeasurementDate <= measurement.MeasurementDate)
            .OrderByDescending(m => m.MeasurementDate).ThenByDescending(m => m.CreatedAt);
        var previous = new BodyReading(
            await earlier.Where(m => m.WeightKg != null).Select(m => m.WeightKg).FirstOrDefaultAsync(cancellationToken),
            await earlier.Where(m => m.BodyFatPercentage != null).Select(m => m.BodyFatPercentage).FirstOrDefaultAsync(cancellationToken),
            await earlier.Where(m => m.MuscleMassKg != null).Select(m => m.MuscleMassKg).FirstOrDefaultAsync(cancellationToken));
        var latest = new BodyReading(measurement.WeightKg, measurement.BodyFatPercentage, measurement.MuscleMassKg);

        var sent = false;
        foreach (var metric in GoalReached.Crossed(targets, previous, latest))
        {
            var (target, logged) = metric switch
            {
                GoalMetric.Weight => ($"{GoalReached.Number(goal.TargetWeight!.Value)} {(string.Equals(goal.WeightUnit, "lb", StringComparison.OrdinalIgnoreCase) ? "lb" : "kg")}", GoalReached.Weight(measurement.WeightKg!.Value, goal.WeightUnit)),
                GoalMetric.BodyFat => ($"{GoalReached.Number(goal.TargetBodyFatPercentage!.Value)}%", $"{GoalReached.Number(measurement.BodyFatPercentage!.Value)}%"),
                _ => ($"{GoalReached.Number(goal.TargetMuscleMassKg!.Value)} kg", $"{GoalReached.Number(measurement.MuscleMassKg!.Value)} kg"),
            };
            var message = NotificationMessages.GoalReached(metric, target, logged);
            var key = DeliveryKey(goal.Id, metric);
            if (await AlreadySentAsync(goal, message.Title, key, cancellationToken)) continue;

            await _notifications.SendAsync(
                new NotificationSend(measurement.UserId, NotificationType, message, NotificationMessages.GoalPath) { DeliveryKey = key },
                cancellationToken);
            sent = true;
        }

        if (sent) await _context.SaveChangesAsync(cancellationToken);
    }

    private static string DeliveryKey(Guid goalId, GoalMetric metric) => $"goal-reached:{goalId:N}:{metric.ToString().ToLowerInvariant()}";

    /// <summary>
    /// Whether this goal's target was already reported: an in-app row since the goal was set, or a
    /// channel job carrying the goal's key, for a person who turned the app off for this kind.
    /// </summary>
    private async Task<bool> AlreadySentAsync(UserGoal goal, string title, string key, CancellationToken cancellationToken)
    {
        string[] channelKeys = [$"push:{key}", $"telegram:notification:{key}", $"email:notification:{key}"];
        return await _context.Notifications.AsNoTracking()
                .AnyAsync(n => n.UserId == goal.UserId && n.Type == NotificationType && n.Title == title && n.CreatedAt >= goal.CreatedAt, cancellationToken)
            || await _context.OutboxJobs.AsNoTracking()
                .AnyAsync(j => j.DedupeKey != null && channelKeys.Contains(j.DedupeKey), cancellationToken);
    }
}
