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
    public const string EstimateSource = "estimate";
    public const string FromCatalogueSource = "catalogue";
    public const string FromRecipeSource = "recipe";

    public string Name { get; init; } = string.Empty;

    /// <summary>The catalogue food this is, when one matches by name; its nutrition is then the catalogue's.</summary>
    public Guid? FoodId { get; init; }

    /// <summary>
    /// The person's own recipe this is, when its title matches; matched before any food. Log it
    /// with the recipe and <see cref="Servings"/>.
    /// </summary>
    public Guid? RecipeId { get; init; }

    /// <summary>Servings of <see cref="RecipeId"/>: from the portion when the recipe's yield is known, else one.</summary>
    public decimal? Servings { get; init; }

    /// <summary>
    /// What the numbers are: "recipe" for the matched recipe's own nutrition at <see cref="Servings"/>,
    /// "catalogue" for the matched food's per 100 g at this portion, else "estimate".
    /// </summary>
    public string Source { get; init; } = EstimateSource;
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

/// <summary>A meal described in words for the assistant to estimate (POST api/Nutrition/ai/analyze-text).</summary>
public record AnalyzeFoodTextRequest(string? Description);

/// <summary>
/// Body of GET /api/Recipes/fit: the person's saved recipes in the order of how well one serving
/// fits what is left of the day's targets (RecipeFit in Domain). A "left" figure is null where
/// there is no target; it is negative when the day is already over.
/// </summary>
public record RecipeFitResult
{
    public DateOnly Date { get; init; }
    public decimal? CaloriesLeft { get; init; }
    public decimal? ProteinLeft { get; init; }
    public decimal? CarbsLeft { get; init; }
    public decimal? FatLeft { get; init; }

    /// <summary>Why nothing is ranked, when nothing is: no target or no recipe with nutrition.</summary>
    public string? Note { get; init; }
    public List<RecipeFitItem> Recipes { get; init; } = new();
}

/// <summary>One recipe, best first: one serving's nutrition and what would be left after it.</summary>
public record RecipeFitItem
{
    public int Rank { get; init; }
    public Guid RecipeId { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal Score { get; init; }
    public decimal Calories { get; init; }
    public decimal Protein { get; init; }
    public decimal Carbs { get; init; }
    public decimal Fat { get; init; }
    public decimal? CaloriesLeftAfter { get; init; }
    public decimal? ProteinLeftAfter { get; init; }
    public decimal? CarbsLeftAfter { get; init; }
    public decimal? FatLeftAfter { get; init; }
}
