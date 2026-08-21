using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Recipes;

namespace Mizan.Application.Common;

/// <summary>
/// Loads computed nutrition for a set of recipes in two queries.
///
/// Replaces the recipe_nutrition table - see docs/REFOCUS.md §4. Stored totals
/// went stale the moment an ingredient changed and nobody recalculated; these
/// are derived every time, so they cannot.
/// </summary>
public static class RecipeNutritionLookup
{
    /// <summary>Totals per serving, keyed by recipe id. Recipes with no resolvable ingredients yield zeroes.</summary>
    public static async Task<IReadOnlyDictionary<Guid, RecipeNutritionTotals>> ForRecipesAsync(
        IMizanDbContext context,
        IReadOnlyCollection<Guid> recipeIds,
        CancellationToken cancellationToken)
    {
        if (recipeIds.Count == 0)
        {
            return new Dictionary<Guid, RecipeNutritionTotals>();
        }

        var recipes = await context.Recipes
            .AsNoTracking()
            .Where(r => recipeIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Servings, Ingredients = r.Ingredients.ToList() })
            .ToListAsync(cancellationToken);

        var foodIds = recipes
            .SelectMany(r => r.Ingredients)
            .Where(i => i.FoodId.HasValue)
            .Select(i => i.FoodId!.Value)
            .Distinct()
            .ToList();

        var foods = await context.Foods
            .AsNoTracking()
            .Where(f => foodIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        return recipes.ToDictionary(
            r => r.Id,
            r => RecipeNutritionCalculator
                .Sum(r.Ingredients, foods, out _)
                .PerServing(r.Servings));
    }

    /// <summary>Totals per serving for one recipe.</summary>
    public static async Task<RecipeNutritionTotals> ForRecipeAsync(
        IMizanDbContext context,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        var byId = await ForRecipesAsync(context, new[] { recipeId }, cancellationToken);
        return byId.TryGetValue(recipeId, out var totals) ? totals : default;
    }
}
