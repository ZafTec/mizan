namespace Mizan.Application.Services;

/// <summary>
/// Pure functions for checking goal reasonableness using nutrition science guidelines.
/// Helps users avoid setting unrealistic or unhealthy goals.
/// Results are informational hints: goals are always created regardless.
/// </summary>
public static class GoalHints
{
    private const decimal ProteinKcalPerGram = 4m;
    private const decimal CarbsKcalPerGram = 4m;
    private const decimal FatKcalPerGram = 9m;

    public const int MinSafeCalories = 800;
    public const int MaxTypicalCalories = 5000;
    public const decimal MacroDeviationThreshold = 0.20m;
    public const decimal MaxProteinPercent = 0.40m;
    public const decimal MaxCarbsPercent = 0.70m;
    public const decimal MinFatPercent = 0.15m;

    public static IReadOnlyList<string> CheckGoalSanity(
        string? goalType,
        int? targetCalories,
        decimal? targetProteinGrams,
        decimal? targetCarbsGrams,
        decimal? targetFatGrams,
        DateOnly? targetDate,
        decimal? targetBodyFatPercentage = null,
        decimal? targetMuscleMassKg = null,
        decimal? targetProteinCalorieRatio = null)
    {
        var hints = new List<string>();

        if (targetDate.HasValue && targetDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            hints.Add("Target date should be in the future, not today or in the past.");
        }

        if (targetCalories.HasValue)
        {
            if (targetCalories.Value < MinSafeCalories)
            {
                hints.Add(
                    $"Target calories ({targetCalories} kcal/day) is very low (below {MinSafeCalories} kcal). " +
                    "Such restrictive diets should only be followed under medical supervision.");
            }
            else if (targetCalories.Value > MaxTypicalCalories)
            {
                hints.Add(
                    $"Target calories ({targetCalories} kcal/day) is very high (above {MaxTypicalCalories} kcal). " +
                    "Most people need 1500–3500 kcal/day. Verify this is intentional.");
            }
        }

        if (targetCalories.HasValue && targetProteinGrams.HasValue && targetCarbsGrams.HasValue && targetFatGrams.HasValue)
        {
            var atwaterEstimate = (targetProteinGrams.Value * ProteinKcalPerGram)
                                + (targetCarbsGrams.Value * CarbsKcalPerGram)
                                + (targetFatGrams.Value * FatKcalPerGram);

            var reference = atwaterEstimate > 0 ? atwaterEstimate : targetCalories.Value;
            if (reference > 0)
            {
                var deviation = Math.Abs(targetCalories.Value - atwaterEstimate) / reference;
                if (deviation > MacroDeviationThreshold)
                {
                    hints.Add(
                        $"Your target calories ({targetCalories} kcal) differ significantly from your macro totals " +
                        $"({atwaterEstimate:F0} kcal via protein×4 + carbs×4 + fat×9). Verify your targets are consistent.");
                }
            }
        }

        if (!string.IsNullOrEmpty(goalType) && targetCalories.HasValue && targetCalories.Value > 0)
        {
            switch (goalType.ToLower())
            {
                case "weight_loss" when targetCalories.Value > 3500:
                    hints.Add(
                        $"For weight loss, {targetCalories} kcal/day is above typical maintenance for most people. " +
                        "Consider whether this supports your goal.");
                    break;

                case "muscle_gain" when targetProteinGrams.HasValue:
                    var muscleProteinPct = (targetProteinGrams.Value * ProteinKcalPerGram) / targetCalories.Value;
                    if (muscleProteinPct < 0.10m)
                    {
                        hints.Add(
                            $"For muscle gain, protein should be at least 10% of calories. " +
                            $"Your target is {muscleProteinPct:P0} ({targetProteinGrams}g ÷ {targetCalories} kcal). " +
                            "Consider increasing protein.");
                    }
                    break;
            }
        }

        if (targetProteinGrams.HasValue && targetCarbsGrams.HasValue && targetFatGrams.HasValue
            && targetCalories.HasValue && targetCalories.Value > 0)
        {
            var proteinPercent = (targetProteinGrams.Value * ProteinKcalPerGram) / targetCalories.Value;
            var carbsPercent = (targetCarbsGrams.Value * CarbsKcalPerGram) / targetCalories.Value;
            var fatPercent = (targetFatGrams.Value * FatKcalPerGram) / targetCalories.Value;

            if (proteinPercent > MaxProteinPercent)
            {
                hints.Add(
                    $"Protein is {proteinPercent:P0} of your calories, above the recommended 40% upper limit. " +
                    "Most guidelines suggest keeping protein under 35%. This may be intentional if following a specific protocol.");
            }

            if (carbsPercent > MaxCarbsPercent)
            {
                hints.Add(
                    $"Carbs are {carbsPercent:P0} of your calories, above the 70% threshold. " +
                    "Most guidelines recommend 45–65%. Consider if this aligns with your dietary plan.");
            }

            if (fatPercent < MinFatPercent)
            {
                hints.Add(
                    $"Fat is {fatPercent:P0} of your calories, below the recommended 15% minimum. " +
                    "Very low fat intake may affect hormone production and fat-soluble vitamin absorption.");
            }
        }

        if (targetBodyFatPercentage.HasValue)
        {
            if (targetBodyFatPercentage.Value < 5m)
            {
                hints.Add("Target body fat below 5% is dangerously low for most people.");
            }
            else if (targetBodyFatPercentage.Value > 40m)
            {
                hints.Add("Target body fat above 40% may indicate health risks.");
            }
        }

        if (targetMuscleMassKg.HasValue && targetMuscleMassKg.Value > 120m)
        {
            hints.Add("Target muscle mass seems unusually high (above 120 kg).");
        }

        if (targetProteinCalorieRatio.HasValue)
        {
            if (targetProteinCalorieRatio.Value < 10m)
            {
                hints.Add("Very low protein-to-calorie ratio; consider at least 15-20%.");
            }
            else if (targetProteinCalorieRatio.Value > 60m)
            {
                hints.Add("Protein-to-calorie ratio above 60% is extreme.");
            }
        }

        if (targetProteinCalorieRatio.HasValue && targetCalories.HasValue && targetCalories.Value > 0 && targetProteinGrams.HasValue)
        {
            var impliedRatio = targetProteinGrams.Value * ProteinKcalPerGram / targetCalories.Value * 100m;
            var reference = Math.Max(impliedRatio, targetProteinCalorieRatio.Value);
            if (reference > 0)
            {
                var deviation = Math.Abs(impliedRatio - targetProteinCalorieRatio.Value) / reference;
                if (deviation > MacroDeviationThreshold)
                {
                    hints.Add(
                        $"Your target P/Cal ratio ({targetProteinCalorieRatio.Value:F0}%) differs significantly from the ratio implied by your protein/calorie targets ({impliedRatio:F0}%).");
                }
            }
        }

        return hints.AsReadOnly();
    }
}
