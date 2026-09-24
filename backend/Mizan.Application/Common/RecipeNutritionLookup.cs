using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Application.Exceptions;
using Mizan.Domain.Recipes;

namespace Mizan.Application.Common;

/// <summary>
/// Why a recipe does or does not have nutrition. <see cref="PerServing"/> is
/// set only when every measured ingredient resolves; <see cref="Unmeasured"/>
/// lists the notes left out of it, and <see cref="Unresolved"/> the measured
/// lines that stop it.
/// </summary>
public sealed record RecipeNutritionStatus(
    RecipeNutritionTotals? PerServing,
    IReadOnlyList<string> Unresolved,
    IReadOnlyList<string> Unmeasured);

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
        var statuses = await StatusesAsync(context, recipeIds, cancellationToken);
        return statuses
            .Where(s => s.Value.PerServing.HasValue)
            .ToDictionary(s => s.Key, s => s.Value.PerServing!.Value);
    }

    /// <summary>Nutrition and the reasons behind it, keyed by recipe id.</summary>
    public static async Task<IReadOnlyDictionary<Guid, RecipeNutritionStatus>> StatusesAsync(
        IMizanDbContext context,
        IReadOnlyCollection<Guid> recipeIds,
        CancellationToken cancellationToken)
    {
        if (recipeIds.Count == 0)
        {
            return new Dictionary<Guid, RecipeNutritionStatus>();
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

        return recipes.ToDictionary(r => r.Id, r =>
        {
            var ordered = r.Ingredients.OrderBy(i => i.SortOrder).ToList();
            var totals = RecipeNutritionCalculator.Sum(ordered, foods, out var unresolved, out var unmeasured);
            var measured = ordered.Count - unmeasured.Count;
            var complete = r.Servings > 0 && measured > 0 && unresolved.Count == 0;
            return new RecipeNutritionStatus(complete ? totals.PerServing(r.Servings) : null, unresolved, unmeasured);
        });
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
