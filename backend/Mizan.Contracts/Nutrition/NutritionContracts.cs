namespace Mizan.Contracts.Nutrition;

/// <summary>
/// The body of GET /api/Nutrition/daily, as far as a service client needs it:
/// what was eaten against what was aimed for. The API's own result adds the
/// date and a per-meal breakdown on top.
/// </summary>
public record DailyNutritionTotals
{
    public decimal TotalCalories { get; init; }
    public decimal TotalProtein { get; init; }
    public decimal TotalCarbs { get; init; }
    public decimal TotalFat { get; init; }
    public decimal? TargetCalories { get; init; }
    public decimal? TargetProtein { get; init; }
    public decimal? TargetCarbs { get; init; }
    public decimal? TargetFat { get; init; }
}

/// <summary>
/// Body of the POST /api/Nutrition/ai/analyze-image and analyze-text
/// responses: a proposal the person confirms before anything is logged.
/// </summary>
public record FoodAnalysisResult
{
    public List<RecognizedFood> Foods { get; init; } = new();
    public decimal TotalCalories { get; init; }
    public decimal Confidence { get; init; }

    /// <summary>Caveats from the model. Never the payload - the numbers are.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Where the photo was stored, if storage is configured. The model's
    /// numbers are a guess at a portion size; keeping the picture is what lets
    /// someone check the guess later instead of taking it on trust.
    /// </summary>
    public string? ImageUrl { get; init; }
}

public record RecognizedFood
{
    public string Name { get; init; } = string.Empty;
    public decimal PortionGrams { get; init; }
    public decimal Calories { get; init; }
    public decimal Protein { get; init; }
    public decimal Carbs { get; init; }
    public decimal Fat { get; init; }

    /// <summary>
    /// The same numbers for 100 g, derived from the portion. A client sends
    /// these with the grams the person confirmed and the server does the
    /// scaling, so no client carries its own copy of the arithmetic. Null when
    /// the model gave no portion weight to scale from.
    /// </summary>
    public Meals.NutritionPer100g? Per100g => PortionGrams > 0
        ? new Meals.NutritionPer100g
        {
            Calories = Math.Round(Calories / PortionGrams * 100, 2),
            ProteinGrams = Math.Round(Protein / PortionGrams * 100, 2),
            CarbsGrams = Math.Round(Carbs / PortionGrams * 100, 2),
            FatGrams = Math.Round(Fat / PortionGrams * 100, 2),
        }
        : null;
}
