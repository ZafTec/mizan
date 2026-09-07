using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Application.Exceptions;
using Mizan.Domain.Recipes;

namespace Mizan.Application.Common;

/// <summary>
/// Loads computed nutrition for a set of recipes in two queries.
///
/// Replaces the recipe_nutrition table - see docs/ARCHITECTURE.md#navigation-and-logging. Stored totals
/// went stale the moment an ingredient changed and nobody recalculated; these
/// are derived every time, so they cannot.
/// </summary>
public static class RecipeNutritionLookup
{
    /// <summary>Complete totals per serving, keyed by recipe id. Unresolved recipes are omitted.</summary>
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

        return recipes.Select(r =>
            {
                var totals = RecipeNutritionCalculator.Sum(r.Ingredients, foods, out var unresolved);
                return new { r.Id, Totals = totals.PerServing(r.Servings), Complete = r.Servings > 0 && r.Ingredients.Count > 0 && unresolved.Count == 0 };
            })
            .Where(r => r.Complete)
            .ToDictionary(r => r.Id, r => r.Totals);
    }

    /// <summary>Totals per serving for one recipe.</summary>
    public static async Task<RecipeNutritionTotals> ForRecipeAsync(
        IMizanDbContext context,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        var byId = await ForRecipesAsync(context, new[] { recipeId }, cancellationToken);
        return byId.TryGetValue(recipeId, out var totals) ? totals
            : throw new DomainValidationException("This recipe needs linked ingredients with known weights before its nutrition can be calculated.");
    }
}
