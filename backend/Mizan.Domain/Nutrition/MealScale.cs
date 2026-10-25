using Mizan.Domain.Planning;

namespace Mizan.Domain.Nutrition;

/// <summary>
/// How much of a food or a recipe a line of a meal comes to, rounded to two decimals the way a
/// logged row is stored. Logging an estimate by weight and previewing the meal form both use it.
/// </summary>
public static class MealScale
{
    public static EntryNutrition ForGrams(EntryNutrition per100g, decimal grams) => Scaled(per100g, grams / 100m);

    public static EntryNutrition ForServings(EntryNutrition perServing, decimal servings) => Scaled(perServing, servings);

    private static EntryNutrition Scaled(EntryNutrition basis, decimal factor) => new(
        Math.Round(basis.Calories * factor, 2),
        Math.Round(basis.ProteinGrams * factor, 2),
        Math.Round(basis.CarbsGrams * factor, 2),
        Math.Round(basis.FatGrams * factor, 2),
        Math.Round(basis.FiberGrams * factor, 2));
}
