using Mizan.Domain.Entities;

namespace Mizan.Domain.Recipes;

/// <summary>
/// Recipe nutrition, computed from ingredients rather than stored.
///
/// The recipe_nutrition table held values that drifted the moment an ingredient
/// changed and nobody recalculated - see docs/REFOCUS.md §4. This is a pure
/// function over the ingredients, so it cannot go stale.
///
/// Ingredient amounts are grams; food macros are per 100 g.
/// </summary>
public readonly record struct RecipeNutritionTotals(
    decimal Calories,
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatGrams,
    decimal FiberGrams)
{
    public decimal ProteinCalorieRatio => Food.ComputeProteinCalorieRatio(Calories, ProteinGrams);

    /// <summary>Per serving, for a recipe divided into <paramref name="servings"/>.</summary>
    public RecipeNutritionTotals PerServing(int servings)
    {
        if (servings <= 1) return this;
        var n = (decimal)servings;
        return new RecipeNutritionTotals(
            Math.Round(Calories / n, 2),
            Math.Round(ProteinGrams / n, 2),
            Math.Round(CarbsGrams / n, 2),
            Math.Round(FatGrams / n, 2),
            Math.Round(FiberGrams / n, 2));
    }

    /// <summary>Per 100 g of finished weight. Used to derive a preparation's food.</summary>
    public RecipeNutritionTotals Per100g(decimal yieldGrams)
    {
        var factor = 100m / yieldGrams;
        return new RecipeNutritionTotals(
            Math.Round(Calories * factor, 2),
            Math.Round(ProteinGrams * factor, 2),
            Math.Round(CarbsGrams * factor, 2),
            Math.Round(FatGrams * factor, 2),
            Math.Round(FiberGrams * factor, 2));
    }
}

public static class RecipeNutritionCalculator
{
    /// <summary>
    /// Sums the ingredients. An ingredient with no resolvable food contributes
    /// nothing and is reported in <paramref name="unresolved"/> - callers that
    /// need exact figures, such as deriving a preparation, must refuse rather
    /// than publish a total that silently understates.
    /// </summary>
    public static RecipeNutritionTotals Sum(
        IEnumerable<RecipeIngredient> ingredients,
        IReadOnlyDictionary<Guid, Food> foodsById,
        out IReadOnlyList<string> unresolved)
    {
        decimal calories = 0, protein = 0, carbs = 0, fat = 0, fiber = 0;
        var missing = new List<string>();

        foreach (var ingredient in ingredients)
        {
            if (!ingredient.FoodId.HasValue || !foodsById.TryGetValue(ingredient.FoodId.Value, out var food))
            {
                missing.Add(ingredient.IngredientText);
                continue;
            }

            var scale = (ingredient.Amount ?? 0m) / 100m;
            calories += food.CaloriesPer100g * scale;
            protein += food.ProteinPer100g * scale;
            carbs += food.CarbsPer100g * scale;
            fat += food.FatPer100g * scale;
            fiber += (food.FiberPer100g ?? 0m) * scale;
        }

        unresolved = missing;
        return new RecipeNutritionTotals(
            Math.Round(calories, 2),
            Math.Round(protein, 2),
            Math.Round(carbs, 2),
            Math.Round(fat, 2),
            Math.Round(fiber, 2));
    }
}
