using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Streaks;

namespace Mizan.Infrastructure.Services;

/// <summary>
/// The write half. Every rule about what a day is and what extends a streak
/// lives in <see cref="StreakClock"/>; this only reads the row, applies the
/// transition and saves.
/// </summary>
public class StreakService : IStreakService
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserClock _clock;
    private readonly INotificationWriter? _notifications;

    public StreakService(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IUserClock clock,
        INotificationWriter? notifications = null)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<StreakUpdate> RecordActivityAsync(
        string streakType,
        DateOnly? activityDate = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User must be authenticated to record activity");
        if (string.IsNullOrWhiteSpace(streakType))
        {
            throw new ArgumentException("streakType is required", nameof(streakType));
        }

        // The user's day, not the server's. A backdated entry is accepted
        // within a day of it either way, which covers a clock skew or a
        // request that crossed local midnight in flight.
        var localToday = await _clock.TodayAsync(userId, cancellationToken);
        var today = activityDate ?? localToday;
        if (Math.Abs(today.DayNumber - localToday.DayNumber) > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activityDate), "Activity date must be within one day of today");
        }

        var streak = await _context.Streaks
            .FirstOrDefaultAsync(s => s.UserId == userId && s.StreakType == streakType, cancellationToken);

        if (streak is null)
        {
            streak = new Streak
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                StreakType = streakType,
                CurrentCount = 1,
                LongestCount = 1,
                LastActivityDate = today,
            };
            _context.Streaks.Add(streak);
            await _context.SaveChangesAsync(cancellationToken);
            return new StreakUpdate(streakType, 1, 1, true, true, today, false, 0);
        }

        var transition = StreakClock.Extend(
            streak.CurrentCount, streak.LongestCount, streak.LastActivityDate, streak.FreezesAvailable, today);

        if (!transition.Extended)
        {
            return new StreakUpdate(
                streakType,
                streak.CurrentCount,
                streak.LongestCount,
                false,
                false,
                today,
                false,
                streak.FreezesAvailable);
        }

        streak.CurrentCount = transition.CurrentCount;
        streak.LongestCount = transition.LongestCount;
        streak.FreezesAvailable = transition.FreezesAvailable;
        streak.LastActivityDate = today;

        if (_notifications is not null && Milestones.Contains(transition.CurrentCount))
        {
            await _notifications.AddAsync(
                userId,
                "streak_milestone",
                $"{transition.CurrentCount}-day {streakType} streak",
                "Keep the momentum going.",
                "/achievements",
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new StreakUpdate(
            streakType,
            transition.CurrentCount,
            transition.LongestCount,
            transition.IsNewRecord,
            true,
            today,
            transition.FreezeConsumed,
            transition.FreezesAvailable);
    }

    private static readonly int[] Milestones = [7, 30, 100];
}
