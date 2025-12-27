using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetGoalProgressHistoryQuery : IRequest<GoalProgressHistoryDto>
{
    public int Days { get; init; } = 30; // Last 30 days by default
}

public record GoalProgressHistoryDto
{
    public UserGoalSummaryDto? Goal { get; init; }
    public List<GoalProgressEntryDto> ProgressEntries { get; init; } = new();
}

// UserGoalSummaryDto is defined in GetUserGoalQuery.cs - reusing existing DTO

public record GoalProgressEntryDto
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public decimal ActualCalories { get; init; }
    public decimal ActualProteinGrams { get; init; }
    public decimal ActualCarbsGrams { get; init; }
    public decimal ActualFatGrams { get; init; }
    public decimal? ActualWeight { get; init; }
    public string? Notes { get; init; }
}

public class GetGoalProgressHistoryHandler : IRequestHandler<GetGoalProgressHistoryQuery, GoalProgressHistoryDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetGoalProgressHistoryHandler(IMizanDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<GoalProgressHistoryDto> Handle(GetGoalProgressHistoryQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return new GoalProgressHistoryDto();
        }

        // Get active goal
        var activeGoal = await _context.UserGoals
            .Where(g => g.UserId == userId && g.IsActive)
            .Select(g => new UserGoalSummaryDto(
                g.TargetCalories,
                g.TargetProteinGrams,
                g.TargetCarbsGrams,
                g.TargetFatGrams
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (activeGoal == null)
        {
            return new GoalProgressHistoryDto();
        }

        // Get progress entries for the last N days
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-request.Days));
        var progressEntries = await _context.GoalProgress
            .Where(p => p.UserId == userId && p.Date >= startDate)
            .OrderBy(p => p.Date)
            .Select(p => new GoalProgressEntryDto
            {
                Id = p.Id,
                Date = p.Date,
                ActualCalories = p.ActualCalories,
                ActualProteinGrams = p.ActualProteinGrams,
                ActualCarbsGrams = p.ActualCarbsGrams,
                ActualFatGrams = p.ActualFatGrams,
                ActualWeight = p.ActualWeight,
                Notes = p.Notes
            })
            .ToListAsync(cancellationToken);

        return new GoalProgressHistoryDto
        {
            Goal = activeGoal,
            ProgressEntries = progressEntries
        };
    }
}
