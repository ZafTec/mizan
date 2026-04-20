using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Services;

public class StreakService : IStreakService
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StreakService(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<StreakUpdate> RecordActivityAsync(string streakType, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated to record activity");
        }

        if (string.IsNullOrWhiteSpace(streakType))
        {
            throw new ArgumentException("streakType is required", nameof(streakType));
        }

        var userId = _currentUser.UserId.Value;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var streak = await _context.Streaks
            .FirstOrDefaultAsync(s => s.UserId == userId && s.StreakType == streakType, cancellationToken);

        if (streak is null)
        {
            streak = new Streak
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StreakType = streakType,
                CurrentCount = 1,
                LongestCount = 1,
                LastActivityDate = today
            };
            _context.Streaks.Add(streak);
            await _context.SaveChangesAsync(cancellationToken);
            return new StreakUpdate(streakType, 1, 1, IsNewRecord: true, Extended: true, today);
        }

        if (streak.LastActivityDate == today)
        {
            return new StreakUpdate(
                streakType,
                streak.CurrentCount,
                streak.LongestCount,
                IsNewRecord: false,
                Extended: false,
                today);
        }

        var daysSince = streak.LastActivityDate.HasValue
            ? today.DayNumber - streak.LastActivityDate.Value.DayNumber
            : int.MaxValue;

        streak.CurrentCount = daysSince == 1 ? streak.CurrentCount + 1 : 1;
        streak.LastActivityDate = today;

        var isNewRecord = false;
        if (streak.CurrentCount > streak.LongestCount)
        {
            streak.LongestCount = streak.CurrentCount;
            isNewRecord = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new StreakUpdate(
            streakType,
            streak.CurrentCount,
            streak.LongestCount,
            isNewRecord,
            Extended: true,
            today);
    }
}
