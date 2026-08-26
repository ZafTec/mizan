using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
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
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public GetMealPlanByIdQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<MealPlanDetailDto?> Handle(GetMealPlanByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        // A shared plan is visible to the owner and every household member,
        // and each gets a plain "not found" if they are neither - so, like
        // GetRecipeByIdQuery, the viewer's id is part of the key. The tag is
        // the plan's own id rather than the viewer's, so one edit by anyone
        // invalidates every viewer's cached copy, not just the editor's.
        return await _cache.GetOrCreateAsync(
            $"mealplan:{request.Id}:{_currentUser.UserId}",
            request,
            LoadAsync,
            CacheOptions,
            tags: [CacheTags.MealPlan(request.Id)],
            cancellationToken: cancellationToken);
    }

    private async ValueTask<MealPlanDetailDto?> LoadAsync(GetMealPlanByIdQuery request, CancellationToken cancellationToken)
    {
        var mealPlan = await _context.MealPlans
            .Include(mp => mp.MealPlanRecipes)
                .ThenInclude(mpr => mpr.Recipe)
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

        // Summed from ingredients; recipe_nutrition no longer exists (§4).
        var totalsById = await RecipeNutritionLookup.ForRecipesAsync(
            _context,
            mealPlan.MealPlanRecipes.Select(mpr => mpr.RecipeId).Distinct().ToList(),
            cancellationToken);

        decimal PerServing(Guid recipeId, Func<Domain.Recipes.RecipeNutritionTotals, decimal> pick)
            => totalsById.TryGetValue(recipeId, out var t) ? pick(t) : 0m;

        var recipes = mealPlan.MealPlanRecipes.Select(mpr => new MealPlanRecipeDetailDto
        {
            Id = mpr.Id,
            RecipeId = mpr.RecipeId,
            RecipeTitle = mpr.Recipe.Title,
            RecipeImageUrl = mpr.Recipe.ImageUrl,
            Date = mpr.Date,
            MealType = mpr.MealType,
            Servings = mpr.Servings,
            CaloriesPerServing = PerServing(mpr.RecipeId, t => t.Calories)
        }).OrderBy(r => r.Date).ThenBy(r => r.MealType).ToList();

        var totalCalories = mealPlan.MealPlanRecipes.Sum(mpr =>
            PerServing(mpr.RecipeId, t => t.Calories) * mpr.Servings);
        var totalProtein = mealPlan.MealPlanRecipes.Sum(mpr =>
            PerServing(mpr.RecipeId, t => t.ProteinGrams) * mpr.Servings);
        var totalCarbs = mealPlan.MealPlanRecipes.Sum(mpr =>
            PerServing(mpr.RecipeId, t => t.CarbsGrams) * mpr.Servings);
        var totalFat = mealPlan.MealPlanRecipes.Sum(mpr =>
            PerServing(mpr.RecipeId, t => t.FatGrams) * mpr.Servings);

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
