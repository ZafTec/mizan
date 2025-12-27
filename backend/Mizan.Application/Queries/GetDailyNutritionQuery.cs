using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetDailyNutritionQuery : IRequest<DailyNutritionResult>
{
    public DateOnly Date { get; init; }
}

public record DailyNutritionResult
{
    public DateOnly Date { get; init; }
    public decimal TotalCalories { get; init; }
    public decimal TotalProtein { get; init; }
    public decimal TotalCarbs { get; init; }
    public decimal TotalFat { get; init; }
    public decimal? TargetCalories { get; init; }
    public decimal? TargetProtein { get; init; }
    public decimal? TargetCarbs { get; init; }
    public decimal? TargetFat { get; init; }
    public List<MealSummary> MealBreakdown { get; init; } = new();
}

public record MealSummary
{
    public string MealType { get; init; } = string.Empty;
    public decimal Calories { get; init; }
    public decimal Protein { get; init; }
    public decimal Carbs { get; init; }
    public decimal Fat { get; init; }
    public int ItemCount { get; init; }
}

public class GetDailyNutritionQueryHandler : IRequestHandler<GetDailyNutritionQuery, DailyNutritionResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetDailyNutritionQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DailyNutritionResult> Handle(GetDailyNutritionQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var entries = await _context.FoodDiaryEntries
            .Where(e => e.UserId == _currentUser.UserId && e.EntryDate == request.Date)
            .ToListAsync(cancellationToken);

        var goal = await _context.UserGoals
            .Where(g => g.UserId == _currentUser.UserId && g.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        var mealBreakdown = entries
            .GroupBy(e => e.MealType)
            .Select(g => new MealSummary
            {
                MealType = g.Key,
                Calories = g.Sum(e => e.Calories ?? 0),
                Protein = g.Sum(e => e.ProteinGrams ?? 0),
                Carbs = g.Sum(e => e.CarbsGrams ?? 0),
                Fat = g.Sum(e => e.FatGrams ?? 0),
                ItemCount = g.Count()
            })
            .ToList();

        return new DailyNutritionResult
        {
            Date = request.Date,
            TotalCalories = entries.Sum(e => e.Calories ?? 0),
            TotalProtein = entries.Sum(e => e.ProteinGrams ?? 0),
            TotalCarbs = entries.Sum(e => e.CarbsGrams ?? 0),
            TotalFat = entries.Sum(e => e.FatGrams ?? 0),
            TargetCalories = goal?.TargetCalories,
            TargetProtein = goal?.TargetProteinGrams,
            TargetCarbs = goal?.TargetCarbsGrams,
            TargetFat = goal?.TargetFatGrams,
            MealBreakdown = mealBreakdown
        };
    }
}
