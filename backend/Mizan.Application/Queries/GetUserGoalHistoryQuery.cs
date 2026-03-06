using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetUserGoalHistoryQuery : IRequest<List<UserGoalDto>>;

public class GetUserGoalHistoryQueryHandler : IRequestHandler<GetUserGoalHistoryQuery, List<UserGoalDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUserGoalHistoryQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<UserGoalDto>> Handle(GetUserGoalHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return [];

        var goals = await _context.UserGoals
            .Where(g => g.UserId == _currentUser.UserId)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new UserGoalDto
            {
                Id = g.Id,
                GoalType = g.GoalType,
                TargetCalories = g.TargetCalories,
                TargetProteinGrams = g.TargetProteinGrams,
                TargetCarbsGrams = g.TargetCarbsGrams,
                TargetFatGrams = g.TargetFatGrams,
                TargetFiberGrams = g.TargetFiberGrams,
                TargetWeight = g.TargetWeight,
                WeightUnit = g.WeightUnit,
                TargetBodyFatPercentage = g.TargetBodyFatPercentage,
                TargetMuscleMassKg = g.TargetMuscleMassKg,
                TargetProteinCalorieRatio = g.TargetProteinCalorieRatio,
                TargetDate = g.TargetDate,
                IsActive = g.IsActive,
                CreatedAt = g.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return goals;
    }
}
