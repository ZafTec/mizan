using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record UpdateStreakCommand : IRequest<UpdateStreakResult>
{
    public string StreakType { get; init; } = "nutrition"; // nutrition, workout
}

public record UpdateStreakResult
{
    public int CurrentStreak { get; init; }
    public int LongestStreak { get; init; }
    public bool IsNewRecord { get; init; }
}

public class UpdateStreakCommandHandler : IRequestHandler<UpdateStreakCommand, UpdateStreakResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateStreakCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateStreakResult> Handle(UpdateStreakCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var streak = await _context.Streaks
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId && s.StreakType == request.StreakType, cancellationToken);

        var isNewRecord = false;

        if (streak == null)
        {
            streak = new Streak
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId.Value,
                StreakType = request.StreakType,
                CurrentCount = 1,
                LongestCount = 1,
                LastActivityDate = today
            };
            _context.Streaks.Add(streak);
            isNewRecord = true;
        }
        else
        {
            // Already logged today
            if (streak.LastActivityDate == today)
            {
                return new UpdateStreakResult
                {
                    CurrentStreak = streak.CurrentCount,
                    LongestStreak = streak.LongestCount,
                    IsNewRecord = false
                };
            }

            var daysSinceLastActivity = streak.LastActivityDate.HasValue
                ? today.DayNumber - streak.LastActivityDate.Value.DayNumber
                : int.MaxValue;

            if (daysSinceLastActivity == 1)
            {
                // Consecutive day - increment streak
                streak.CurrentCount++;
            }
            else
            {
                // Streak broken - start fresh
                streak.CurrentCount = 1;
            }

            streak.LastActivityDate = today;

            if (streak.CurrentCount > streak.LongestCount)
            {
                streak.LongestCount = streak.CurrentCount;
                isNewRecord = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateStreakResult
        {
            CurrentStreak = streak.CurrentCount,
            LongestStreak = streak.LongestCount,
            IsNewRecord = isNewRecord
        };
    }
}
