using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Data;

/// <summary>
/// Keeps <c>user_activity_counters</c> in step with what was actually written.
///
/// Deliberately an interceptor rather than a line in each command handler.
/// There are ten write paths today and there will be more; the failure mode of
/// the per-handler version is that someone adds an eleventh, forgets the
/// increment, and an achievement quietly stops unlocking. Nobody notices for
/// months. Reading the change tracker cannot be forgotten.
///
/// Runs after the save, so a crash in the gap leaves a counter one behind. The
/// backfill exists for exactly that, and a counter that is one low costs a
/// badge one meal of delay - the safe direction.
/// </summary>
public class ActivityCounterInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Pending work is keyed by context rather than held in a field, so this
    /// can be a singleton. It has to be: EF resolves interceptors while it
    /// builds <c>DbContextOptions</c>, and asking the container for a scoped
    /// service at that point re-enters it and deadlocks.
    /// </summary>
    private static readonly ConditionalWeakTable<DbContext, List<Adjustment>> Pending = new();

    private sealed record Adjustment(Guid UserId, ActivityCounter Counter, int Delta);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        // Collected before the save: the change tracker no longer says what was
        // added by the time SavedChangesAsync runs.
        if (eventData.Context is { } context)
        {
            Pending.Remove(context);
            var adjustments = Collect(context);
            if (adjustments.Count > 0) Pending.Add(context, adjustments);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && Pending.TryGetValue(context, out var adjustments))
        {
            Pending.Remove(context);

            // The context is right here, so the adjustment rides the same
            // connection. Injecting a service instead would be the deadlock
            // described above.
            foreach (var adjustment in adjustments)
            {
                await context.Database.ExecuteSqlRawAsync(
                    ActivityCounterSql.Adjust(adjustment.Counter),
                    [adjustment.UserId, adjustment.Delta],
                    cancellationToken);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } context) Pending.Remove(context);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) Pending.Remove(context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static List<Adjustment> Collect(DbContext context)
    {
        var pending = new List<Adjustment>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            var delta = entry.State switch
            {
                EntityState.Added => 1,
                EntityState.Deleted => -1,
                _ => 0,
            };
            if (delta == 0) continue;

            var counted = entry.Entity switch
            {
                FoodDiaryEntry e => (e.UserId, ActivityCounter.Meals),
                // A recipe with no owner is a seeded global one; it belongs to
                // nobody's count.
                Recipe { UserId: { } owner } => (owner, ActivityCounter.Recipes),
                Workout e => (e.UserId, ActivityCounter.Workouts),
                BodyMeasurement e => (e.UserId, ActivityCounter.BodyMeasurements),
                GoalProgress e => (e.UserId, ActivityCounter.GoalProgress),
                _ => ((Guid, ActivityCounter)?)null,
            };

            if (counted is { } hit) pending.Add(new Adjustment(hit.Item1, hit.Item2, delta));
        }

        return pending;
    }
}
