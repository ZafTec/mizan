using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetUserGoalQuery : IRequest<UserGoalDto?>;

public record UserGoalDto
{
    public Guid Id { get; init; }
    public string? GoalType { get; init; }
    public decimal? TargetCalories { get; init; }
    public decimal? TargetProteinGrams { get; init; }
    public decimal? TargetCarbsGrams { get; init; }
    public decimal? TargetFatGrams { get; init; }
    public decimal? TargetWeight { get; init; }
    public string? WeightUnit { get; init; }
    public DateOnly? TargetDate { get; init; }
    public bool IsActive { get; init; }
}

public class GetUserGoalQueryHandler : IRequestHandler<GetUserGoalQuery, UserGoalDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUserGoalQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UserGoalDto?> Handle(GetUserGoalQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return null;

        var goal = await _context.UserGoals
            .Where(g => g.UserId == _currentUser.UserId && g.IsActive)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (goal == null)
            return null;

        return new UserGoalDto
        {
            Id = goal.Id,
            GoalType = goal.GoalType,
            TargetCalories = goal.TargetCalories,
            TargetProteinGrams = goal.TargetProteinGrams,
            TargetCarbsGrams = goal.TargetCarbsGrams,
            TargetFatGrams = goal.TargetFatGrams,
            TargetWeight = goal.TargetWeight,
            WeightUnit = goal.WeightUnit,
            TargetDate = goal.TargetDate,
            IsActive = goal.IsActive
        };
    }
}
