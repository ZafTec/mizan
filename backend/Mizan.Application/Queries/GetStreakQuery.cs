using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetStreakQuery : IRequest<GetStreakResult>
{
    public string StreakType { get; init; } = "nutrition";
}

public record GetStreakResult
{
    public string StreakType { get; init; } = "nutrition";
    public int CurrentStreak { get; init; }
    public int LongestStreak { get; init; }
    public DateOnly? LastActivityDate { get; init; }
    public bool IsActiveToday { get; init; }
    public int FreezesAvailable { get; init; }

    /// <summary>
    /// Local midnight, as an instant. The screen counts down to this rather
    /// than showing a flame with no deadline attached.
    /// </summary>
    public DateTimeOffset ResetsAt { get; init; }

    /// <summary>The zone the deadline is in, so the UI can name it.</summary>
    public string TimeZoneId { get; init; } = "UTC";

    /// <summary>Alive, but today has not been logged yet.</summary>
    public bool AtRisk { get; init; }
}

public class GetStreakQueryHandler : IRequestHandler<GetStreakQuery, GetStreakResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserClock _clock;

    public GetStreakQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, IUserClock clock)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<GetStreakResult> Handle(GetStreakQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User must be authenticated");

        var streakType = string.IsNullOrWhiteSpace(request.StreakType) ? "nutrition" : request.StreakType;

        var streak = await _context.Streaks.AsNoTracking()
            .Where(s => s.UserId == userId && s.StreakType == streakType)
            .Select(s => new { s.CurrentCount, s.LongestCount, s.LastActivityDate, s.FreezesAvailable })
            .FirstOrDefaultAsync(cancellationToken);

        // The stored row is a record of the last write, not of what is true
        // now. StreakClock decides whether it has lapsed since - the same
        // function the writer and every other reader uses.
        var state = await _clock.EvaluateAsync(
            userId,
            streak?.CurrentCount ?? 0,
            streak?.LongestCount ?? 0,
            streak?.LastActivityDate,
            streak?.FreezesAvailable ?? 0,
            cancellationToken);

        return new GetStreakResult
        {
            StreakType = streakType,
            CurrentStreak = state.CurrentCount,
            LongestStreak = state.LongestCount,
            LastActivityDate = streak?.LastActivityDate,
            IsActiveToday = state.IsActiveToday,
            FreezesAvailable = state.FreezesAvailable,
            ResetsAt = state.ResetsAt,
            TimeZoneId = await _clock.TimeZoneIdAsync(userId, cancellationToken),
            AtRisk = state.AtRisk,
        };
    }
}
