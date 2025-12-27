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
            .Include(u => u.CurrentGoal)
            .Include(u => u.Streaks)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var currentStreak = user.Streaks
            .OrderByDescending(s => s.LastActivityDate)
            .FirstOrDefault();

        return new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.Image,
            user.CreatedAt,
            user.CurrentGoal != null ? new UserGoalSummaryDto(
                user.CurrentGoal.TargetCalories,
                user.CurrentGoal.TargetProteinGrams,
                user.CurrentGoal.TargetCarbsGrams,
                user.CurrentGoal.TargetFatGrams
            ) : null,
            currentStreak?.CurrentCount ?? 0
        );
    }
}
