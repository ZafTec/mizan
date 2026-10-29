using Mizan.Application.Notifications;
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
    private readonly IAppUrls? _urls;

    public StreakService(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IUserClock clock,
        INotificationWriter? notifications = null,
        IAppUrls? urls = null)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
        _urls = urls;
    }

    public async Task<StreakUpdate?> RecordActivityAsync(
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

        // The user's day, not the server's. The caller has already saved the
        // activity, so a day too far from today to count is no error: the
        // streak is left as it is and the caller reports no streak change.
        var localToday = await _clock.TodayAsync(userId, cancellationToken);
        var today = activityDate ?? localToday;
        if (!StreakClock.Counts(today, localToday)) return null;

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
            await _notifications.SendAsync(
                new NotificationSend(
                    userId,
                    "streak_milestone",
                    NotificationMessages.StreakMilestone(transition.CurrentCount, streakType, _urls?.Asset(NotificationMessages.StreakBadge(transition.CurrentCount))),
                    NotificationMessages.AchievementsPath),
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
