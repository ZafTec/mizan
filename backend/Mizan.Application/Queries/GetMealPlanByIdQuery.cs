using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetMealPlanByIdQuery(Guid Id) : IRequest<MealPlanDetailDto?>;

public record MealPlanDetailDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public List<MealPlanRecipeDetailDto> Recipes { get; init; } = new();
    public MealPlanNutritionSummaryDto NutritionSummary { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record MealPlanRecipeDetailDto
{
    public Guid Id { get; init; }
    public Guid RecipeId { get; init; }
    public string RecipeTitle { get; init; } = string.Empty;
    public string? RecipeImageUrl { get; init; }
    public DateOnly Date { get; init; }
    public string MealType { get; init; } = string.Empty;
    public decimal Servings { get; init; }
    public decimal? CaloriesPerServing { get; init; }
}

public record MealPlanNutritionSummaryDto
{
    public decimal TotalCalories { get; init; }
    public decimal TotalProteinGrams { get; init; }
    public decimal TotalCarbsGrams { get; init; }
    public decimal TotalFatGrams { get; init; }
    public int DaysCount { get; init; }
    public decimal AvgCaloriesPerDay { get; init; }
}

public class GetMealPlanByIdQueryHandler : IRequestHandler<GetMealPlanByIdQuery, MealPlanDetailDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMealPlanByIdQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<MealPlanDetailDto?> Handle(GetMealPlanByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var mealPlan = await _context.MealPlans
            .Include(mp => mp.MealPlanRecipes)
                .ThenInclude(mpr => mpr.Recipe)
                    .ThenInclude(r => r.Nutrition)
            .FirstOrDefaultAsync(mp => mp.Id == request.Id, cancellationToken);

        if (mealPlan == null)
        {
            return null;
        }

        // Authorization: User must own the meal plan OR be a member of the household
        if (!await IsAuthorizedAsync(mealPlan, cancellationToken))
        {
            return null;
        }

        var recipes = mealPlan.MealPlanRecipes.Select(mpr => new MealPlanRecipeDetailDto
        {
            Id = mpr.Id,
            RecipeId = mpr.RecipeId,
            RecipeTitle = mpr.Recipe.Title,
            RecipeImageUrl = mpr.Recipe.ImageUrl,
            Date = mpr.Date,
            MealType = mpr.MealType,
            Servings = mpr.Servings,
            CaloriesPerServing = mpr.Recipe.Nutrition?.CaloriesPerServing
        }).OrderBy(r => r.Date).ThenBy(r => r.MealType).ToList();

        var totalCalories = mealPlan.MealPlanRecipes.Sum(mpr =>
            (mpr.Recipe.Nutrition?.CaloriesPerServing ?? 0) * mpr.Servings);
        var totalProtein = mealPlan.MealPlanRecipes.Sum(mpr =>
            (mpr.Recipe.Nutrition?.ProteinGrams ?? 0) * mpr.Servings);
        var totalCarbs = mealPlan.MealPlanRecipes.Sum(mpr =>
            (mpr.Recipe.Nutrition?.CarbsGrams ?? 0) * mpr.Servings);
        var totalFat = mealPlan.MealPlanRecipes.Sum(mpr =>
            (mpr.Recipe.Nutrition?.FatGrams ?? 0) * mpr.Servings);

        var daysCount = mealPlan.EndDate.DayNumber - mealPlan.StartDate.DayNumber + 1;

        return new MealPlanDetailDto
        {
            Id = mealPlan.Id,
            Name = mealPlan.Name,
            StartDate = mealPlan.StartDate,
            EndDate = mealPlan.EndDate,
            Recipes = recipes,
            NutritionSummary = new MealPlanNutritionSummaryDto
            {
                TotalCalories = totalCalories,
                TotalProteinGrams = totalProtein,
                TotalCarbsGrams = totalCarbs,
                TotalFatGrams = totalFat,
                DaysCount = daysCount,
                AvgCaloriesPerDay = daysCount > 0 ? totalCalories / daysCount : 0
            },
            CreatedAt = mealPlan.CreatedAt,
            UpdatedAt = mealPlan.UpdatedAt
        };
    }

    private async Task<bool> IsAuthorizedAsync(Domain.Entities.MealPlan mealPlan, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            return false;
        }

        // User owns the meal plan
        if (mealPlan.UserId == userId.Value)
        {
            return true;
        }

        // Meal plan belongs to a household and user is a member
        if (mealPlan.HouseholdId.HasValue)
        {
            var isMember = await _context.HouseholdMembers
                .AnyAsync(hm => hm.HouseholdId == mealPlan.HouseholdId.Value && hm.UserId == userId.Value, cancellationToken);
            return isMember;
        }

        return false;
    }
}
