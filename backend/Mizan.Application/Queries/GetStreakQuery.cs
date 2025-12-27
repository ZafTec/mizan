using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetStreakQuery : IRequest<GetStreakResult>;

public record GetStreakResult
{
    public int CurrentStreak { get; init; }
    public int LongestStreak { get; init; }
    public DateOnly? LastActivityDate { get; init; }
    public bool IsActiveToday { get; init; }
}

public class GetStreakQueryHandler : IRequestHandler<GetStreakQuery, GetStreakResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetStreakQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetStreakResult> Handle(GetStreakQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var streak = await _context.Streaks
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId, cancellationToken);

        if (streak == null)
        {
            return new GetStreakResult
            {
                CurrentStreak = 0,
                LongestStreak = 0,
                LastActivityDate = null,
                IsActiveToday = false
            };
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isActiveToday = streak.LastActivityDate == today;

        // Check if streak is still valid (within 1 day)
        var currentStreak = streak.CurrentCount;
        if (streak.LastActivityDate.HasValue)
        {
            var daysSinceLastActivity = today.DayNumber - streak.LastActivityDate.Value.DayNumber;
            if (daysSinceLastActivity > 1)
            {
                currentStreak = 0; // Streak broken
            }
        }

        return new GetStreakResult
        {
            CurrentStreak = currentStreak,
            LongestStreak = streak.LongestCount,
            LastActivityDate = streak.LastActivityDate,
            IsActiveToday = isActiveToday
        };
    }
}
