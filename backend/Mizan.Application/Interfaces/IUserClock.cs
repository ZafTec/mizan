using Mizan.Domain.Streaks;

namespace Mizan.Application.Interfaces;

/// <summary>
/// What day it is for a given user.
///
/// A separate service rather than a join because it sits on the logging path -
/// every meal, workout and measurement needs it - and the answer changes about
/// once a year per user. It is cached alongside the user's status and cleared
/// by the same invalidator.
/// </summary>
public interface IUserClock
{
    Task<string> TimeZoneIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<DateOnly> TodayAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<StreakState> EvaluateAsync(
        Guid userId,
        int currentCount,
        int longestCount,
        DateOnly? lastActivityDate,
        int freezesAvailable,
        CancellationToken cancellationToken = default);
}
