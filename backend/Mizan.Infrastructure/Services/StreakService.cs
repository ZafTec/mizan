using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Services;

public class StreakService : IStreakService
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationWriter? _notifications;

    public StreakService(IMizanDbContext context, ICurrentUserService currentUser, INotificationWriter? notifications = null)
    {
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<StreakUpdate> RecordActivityAsync(string streakType, DateOnly? activityDate = null, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated to record activity");
        if (string.IsNullOrWhiteSpace(streakType)) throw new ArgumentException("streakType is required", nameof(streakType));
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
        var today = activityDate ?? utcToday;
        if (Math.Abs(today.DayNumber - utcToday.DayNumber) > 1) throw new ArgumentOutOfRangeException(nameof(activityDate), "Activity date must be within one day of today");

        var streak = await _context.Streaks.FirstOrDefaultAsync(s => s.UserId == userId && s.StreakType == streakType, cancellationToken);
        if (streak is null)
        {
            streak = new Streak { Id = Guid.NewGuid(), UserId = userId, StreakType = streakType, CurrentCount = 1, LongestCount = 1, LastActivityDate = today };
            _context.Streaks.Add(streak);
            await _context.SaveChangesAsync(cancellationToken);
            return new StreakUpdate(streakType, 1, 1, true, true, today, false, 0);
        }

        if (streak.LastActivityDate == today)
            return new StreakUpdate(streakType, streak.CurrentCount, streak.LongestCount, false, false, today, false, streak.FreezesAvailable);

        var daysSince = streak.LastActivityDate.HasValue ? today.DayNumber - streak.LastActivityDate.Value.DayNumber : int.MaxValue;
        var freezeConsumed = daysSince == 2 && streak.FreezesAvailable > 0;
        if (freezeConsumed) streak.FreezesAvailable--;
        streak.CurrentCount = daysSince == 1 || freezeConsumed ? streak.CurrentCount + 1 : 1;
        streak.LastActivityDate = today;
        var isNewRecord = streak.CurrentCount > streak.LongestCount;
        if (isNewRecord) streak.LongestCount = streak.CurrentCount;
        if (streak.CurrentCount > 0 && streak.CurrentCount % 7 == 0) streak.FreezesAvailable = Math.Min(2, streak.FreezesAvailable + 1);

        if (_notifications is not null && new[] { 7, 30, 100 }.Contains(streak.CurrentCount))
            await _notifications.AddAsync(userId, "streak_milestone", $"{streak.CurrentCount}-day {streakType} streak", "Keep the momentum going.", "/achievements", cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return new StreakUpdate(streakType, streak.CurrentCount, streak.LongestCount, isNewRecord, true, today, freezeConsumed, streak.FreezesAvailable);
    }
}
