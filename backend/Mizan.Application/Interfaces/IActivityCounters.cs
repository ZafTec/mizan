using Mizan.Domain.Entities;

namespace Mizan.Application.Interfaces;

public enum ActivityCounter
{
    Meals = 0,
    Recipes = 1,
    Workouts = 2,
    BodyMeasurements = 3,
    GoalProgress = 4,
}

/// <summary>
/// Running totals, kept without a read.
///
/// The increment is an upsert with the arithmetic done in the database, so two
/// concurrent logs cannot read-modify-write over each other and no round trip
/// is spent fetching the current value.
/// </summary>
public interface IActivityCounters
{
    /// <summary>Adds <paramref name="delta"/>, which is negative on a delete. Never drops below zero.</summary>
    Task AdjustAsync(
        Guid userId, ActivityCounter counter, int delta = 1, CancellationToken cancellationToken = default);

    Task<UserActivityCounters> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
