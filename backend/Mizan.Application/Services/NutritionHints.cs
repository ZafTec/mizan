namespace Mizan.Application.Services;

/// <summary>
/// Pure functions for checking nutritional consistency using Atwater general factors.
/// Protein = 4 kcal/g, Carbohydrates = 4 kcal/g, Fat = 9 kcal/g.
/// Results are informational hints — entries are always saved regardless.
/// </summary>
public static class NutritionHints
{
    private const decimal ProteinKcalPerGram = 4m;
    private const decimal CarbsKcalPerGram = 4m;
    private const decimal FatKcalPerGram = 9m;

    /// <summary>Deviation threshold above which a calorie mismatch hint is generated (20%).</summary>
    public const decimal DeviationThreshold = 0.20m;

    public static IReadOnlyList<string> CheckConsistency(
        decimal? calories,
        decimal? proteinGrams,
        decimal? carbsGrams,
        decimal? fatGrams,
        decimal? fiberGrams)
    {
        var hints = new List<string>();

        if (fiberGrams.HasValue && carbsGrams.HasValue && fiberGrams.Value > carbsGrams.Value)
        {
            hints.Add(
                $"Fiber ({fiberGrams.Value:F1}g) exceeds total carbs ({carbsGrams.Value:F1}g). " +
                "Fiber is a type of carbohydrate and cannot exceed total carbohydrates.");
        }

        if (calories.HasValue && proteinGrams.HasValue && carbsGrams.HasValue && fatGrams.HasValue)
        {
            var atwaterEstimate = (proteinGrams.Value * ProteinKcalPerGram)
                                + (carbsGrams.Value * CarbsKcalPerGram)
                                + (fatGrams.Value * FatKcalPerGram);

            var reference = atwaterEstimate > 0 ? atwaterEstimate : (calories.Value > 0 ? calories.Value : 0);

            if (reference > 0)
            {
                var deviation = Math.Abs(calories.Value - atwaterEstimate) / reference;
                if (deviation > DeviationThreshold)
                {
                    hints.Add(
                        $"Your entered calories ({calories.Value:F0} kcal) differ from the macro-based estimate " +
                        $"({atwaterEstimate:F0} kcal using protein×4 + carbs×4 + fat×9). " +
                        "This may be correct if the food contains alcohol, sugar alcohols, or other non-standard components — " +
                        "otherwise consider reviewing your entries.");
                }
            }
        }

        return hints.AsReadOnly();
    }
}
