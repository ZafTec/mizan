namespace Mizan.Application.Services;

/// <summary>
/// Pure functions for checking goal reasonableness using nutrition science guidelines.
/// Helps users avoid setting unrealistic or unhealthy goals.
/// Results are informational hints — goals are always created regardless.
/// </summary>
public static class GoalHints
{
    private const decimal ProteinKcalPerGram = 4m;
    private const decimal CarbsKcalPerGram = 4m;
    private const decimal FatKcalPerGram = 9m;
    private const decimal DeviationThreshold = 0.20m;

    /// <summary>
    /// Check if a goal's targets are realistic and healthy.
    /// </summary>
    public static IReadOnlyList<string> CheckGoalSanity(
        string? goalType,
        int? targetCalories,
        decimal? targetProteinGrams,
        decimal? targetCarbsGrams,
        decimal? targetFatGrams,
        DateOnly? targetDate)
    {
        var hints = new List<string>();

        // Check target date is in future
        if (targetDate.HasValue && targetDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            hints.Add("Target date should be in the future, not today or in the past.");
        }

        // Check calorie extremes
        if (targetCalories.HasValue)
        {
            if (targetCalories.Value < 800)
            {
                hints.Add(
                    $"Target calories ({targetCalories} kcal) is very low. Diets below 800 kcal/day should only be done under medical supervision.");
            }
            else if (targetCalories.Value > 5000)
            {
                hints.Add(
                    $"Target calories ({targetCalories} kcal) is very high. Most people need 1500-3500 kcal/day. Verify this is intentional.");
            }
        }

        // Check macro-to-calorie consistency
        if (targetCalories.HasValue && targetProteinGrams.HasValue && targetCarbsGrams.HasValue && targetFatGrams.HasValue)
        {
            var atwaterEstimate = (targetProteinGrams.Value * ProteinKcalPerGram)
                                + (targetCarbsGrams.Value * CarbsKcalPerGram)
                                + (targetFatGrams.Value * FatKcalPerGram);

            var reference = atwaterEstimate > 0 ? atwaterEstimate : targetCalories.Value;
            if (reference > 0)
            {
                var deviation = Math.Abs(targetCalories.Value - atwaterEstimate) / reference;
                if (deviation > DeviationThreshold)
                {
                    hints.Add(
                        $"Your target calories ({targetCalories} kcal) differ significantly from macro-based estimate " +
                        $"({atwaterEstimate:F0} kcal via protein×4 + carbs×4 + fat×9). Verify your targets are consistent.");
                }
            }
        }

        // Check goal type coherence
        if (!string.IsNullOrEmpty(goalType) && targetCalories.HasValue)
        {
            switch (goalType.ToLower())
            {
                case "weight_loss":
                    if (targetCalories.Value > 3500)
                    {
                        hints.Add(
                            $"For weight loss, {targetCalories} kcal/day is above typical maintenance for most people. " +
                            "Consider whether this supports your goal.");
                    }
                    break;

                case "muscle_gain":
                    if (targetProteinGrams.HasValue && targetCalories.Value > 0)
                    {
                        var proteinPercent = (targetProteinGrams.Value * ProteinKcalPerGram) / targetCalories.Value;
                        if (proteinPercent < 0.10m) // < 10% from protein
                        {
                            hints.Add(
                                $"For muscle gain, protein should be 10-35% of calories. Your target is {proteinPercent:P0} " +
                                $"({targetProteinGrams}g protein ÷ {targetCalories} kcal). Consider increasing protein.");
                        }
                    }
                    break;

                case "maintenance":
                    // Macro targets should be balanced for maintenance
                    if (targetProteinGrams.HasValue && targetCarbsGrams.HasValue && targetFatGrams.HasValue)
                    {
                        var totalMacroCalories = (targetProteinGrams.Value * ProteinKcalPerGram)
                                              + (targetCarbsGrams.Value * CarbsKcalPerGram)
                                              + (targetFatGrams.Value * FatKcalPerGram);

                        // For maintenance, macros should closely match targets (minimal variance)
                        if (targetCalories.HasValue && targetCalories.Value > 0)
                        {
                            var variance = Math.Abs(totalMacroCalories - targetCalories.Value) / targetCalories.Value;
                            if (variance > 0.15m)
                            {
                                hints.Add(
                                    "For maintenance goals, your macro targets should closely align with total calories. " +
                                    "Small adjustments can help achieve balance.");
                            }
                        }
                    }
                    break;
            }
        }

        // Check macro distribution sanity (basic %)
        if (targetProteinGrams.HasValue && targetCarbsGrams.HasValue && targetFatGrams.HasValue && targetCalories.HasValue && targetCalories.Value > 0)
        {
            var proteinPercent = (targetProteinGrams.Value * ProteinKcalPerGram) / targetCalories.Value;
            var carbsPercent = (targetCarbsGrams.Value * CarbsKcalPerGram) / targetCalories.Value;
            var fatPercent = (targetFatGrams.Value * FatKcalPerGram) / targetCalories.Value;

            if (proteinPercent > 0.40m) // > 40% from protein
            {
                hints.Add(
                    $"Protein is {proteinPercent:P0} of your target calories. Most guidelines recommend 10-35%. " +
                    "Very high protein may be unnecessary unless specifically planned.");
            }

            if (carbsPercent > 0.70m) // > 70% from carbs
            {
                hints.Add(
                    $"Carbs are {carbsPercent:P0} of your target calories. Most guidelines recommend 45-65%. " +
                    "Consider if this aligns with your dietary preference.");
            }

            if (fatPercent < 0.15m) // < 15% from fat
            {
                hints.Add(
                    $"Fat is {fatPercent:P0} of your target calories. Most guidelines recommend 20-35%. " +
                    "Very low fat may impact hormone production and nutrient absorption.");
            }
        }

        return hints.AsReadOnly();
    }
}
