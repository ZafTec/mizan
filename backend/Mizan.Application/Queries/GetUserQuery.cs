using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Queries;

public record GetUserQuery(Guid UserId) : IRequest<UserDto?>;

public record UserDto(
    Guid Id,
    string Email,
    string? Name,
    string? Image,
    DateTime CreatedAt,
    UserGoalSummaryDto? CurrentGoal,
    int StreakCount
);

public record UserGoalSummaryDto(
    decimal? TargetCalories,
    decimal? TargetProteinGrams,
    decimal? TargetCarbsGrams,
    decimal? TargetFatGrams
);

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto?>
{
    private readonly IMizanDbContext _context;

    public GetUserQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<UserDto?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.Goals)
            .Include(u => u.Streaks)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var currentGoal = user.Goals.FirstOrDefault(g => g.IsActive);

        var currentStreak = user.Streaks
            .OrderByDescending(s => s.LastActivityDate)
            .FirstOrDefault();

        return new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.Image,
            user.CreatedAt,
            currentGoal != null ? new UserGoalSummaryDto(
                currentGoal.TargetCalories,
                currentGoal.TargetProteinGrams,
                currentGoal.TargetCarbsGrams,
                currentGoal.TargetFatGrams
            ) : null,
            currentStreak?.CurrentCount ?? 0
        );
    }
}
