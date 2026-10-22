using Mizan.Domain.Entities;
using Mizan.Domain.Recipes;

namespace Mizan.Domain.Planning;

/// <summary>Nutrition of one entry, or null fields when it cannot be resolved.</summary>
public readonly record struct EntryNutrition(
    decimal Calories,
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatGrams,
    decimal FiberGrams)
{
    public static EntryNutrition Zero => new(0, 0, 0, 0, 0);

    public EntryNutrition Add(EntryNutrition other) => new(
        Calories + other.Calories,
        ProteinGrams + other.ProteinGrams,
        CarbsGrams + other.CarbsGrams,
        FatGrams + other.FatGrams,
        FiberGrams + other.FiberGrams);

    public EntryNutrition Round() => new(
        Math.Round(Calories, 1),
        Math.Round(ProteinGrams, 1),
        Math.Round(CarbsGrams, 1),
        Math.Round(FatGrams, 1),
        Math.Round(FiberGrams, 1));
}

/// <summary>The totals of one plan day and the entries that could not be counted.</summary>
public sealed record PlanDayNutrition(
    int DayIndex,
    EntryNutrition Totals,
    IReadOnlyList<Guid> UnresolvedEntryIds);

/// <summary>
/// Sums plan entries into days. Recipe entries need their per-serving totals
/// (the Application layer resolves those, including retained snapshots); food
/// entries use the food's per-100 g values; text entries have no nutrition and
/// are reported as unresolved so no total silently understates.
/// </summary>
public static class PlanNutrition
{
    public static EntryNutrition? Of(
        PlanEntry entry,
        IReadOnlyDictionary<Guid, RecipeNutritionTotals> recipePerServing,
        IReadOnlyDictionary<Guid, Food> foodsById)
    {
        switch (entry.Kind)
        {
            case PlanEntryKind.Recipe when entry.RecipeId is { } recipeId && recipePerServing.TryGetValue(recipeId, out var perServing):
                {
                    var servings = entry.Servings ?? 1m;
                    return new EntryNutrition(
                        perServing.Calories * servings,
                        perServing.ProteinGrams * servings,
                        perServing.CarbsGrams * servings,
                        perServing.FatGrams * servings,
                        perServing.FiberGrams * servings);
                }
            case PlanEntryKind.Food when entry.FoodId is { } foodId && entry.Grams is { } grams && grams > 0 && foodsById.TryGetValue(foodId, out var food):
                {
                    var scale = grams / 100m;
                    return new EntryNutrition(
                        food.CaloriesPer100g * scale,
                        food.ProteinPer100g * scale,
                        food.CarbsPer100g * scale,
                        food.FatPer100g * scale,
                        (food.FiberPer100g ?? 0m) * scale);
                }
            default:
                return null;
        }
    }

    public static IReadOnlyList<PlanDayNutrition> ByDay(
        IEnumerable<PlanEntry> entries,
        int durationDays,
        IReadOnlyDictionary<Guid, RecipeNutritionTotals> recipePerServing,
        IReadOnlyDictionary<Guid, Food> foodsById)
    {
        var days = new List<PlanDayNutrition>(durationDays);
        var grouped = entries.GroupBy(e => e.DayIndex).ToDictionary(g => g.Key, g => g.ToList());
        for (var day = 0; day < durationDays; day++)
        {
            var totals = EntryNutrition.Zero;
            var unresolved = new List<Guid>();
            if (grouped.TryGetValue(day, out var dayEntries))
            {
                foreach (var entry in dayEntries)
                {
                    var nutrition = Of(entry, recipePerServing, foodsById);
                    if (nutrition is null) unresolved.Add(entry.Id);
                    else totals = totals.Add(nutrition.Value);
                }
            }
            days.Add(new PlanDayNutrition(day, totals.Round(), unresolved));
        }
        return days;
    }
}
