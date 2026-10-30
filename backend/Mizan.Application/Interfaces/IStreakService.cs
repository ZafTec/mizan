namespace Mizan.Application.Interfaces;

public interface IStreakService
{
    /// <summary>
    /// Records a day of activity for the given streak type against the currently-authenticated user.
    /// Idempotent within a single day: if the user already has activity logged today for this streak type,
    /// returns the current state without double-incrementing. Null when the day is too far from
    /// the user's today to move a streak (<see cref="Mizan.Domain.Streaks.StreakClock.Counts"/>).
    /// </summary>
    Task<StreakUpdate?> RecordActivityAsync(string streakType, DateOnly? activityDate = null, CancellationToken cancellationToken = default);
}

public record StreakUpdate(
    string StreakType,
    int CurrentCount,
    int LongestCount,
    bool IsNewRecord,
    bool Extended,
    DateOnly LastActivityDate,
    bool FreezeConsumed = false,
    int FreezesAvailable = 0);
