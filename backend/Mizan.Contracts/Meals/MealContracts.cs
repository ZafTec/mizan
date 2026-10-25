namespace Mizan.Contracts.Meals;

/// <summary>
/// Body of POST /api/Meals. Either FoodId or RecipeId identifies what was
/// eaten; the macro fields let a caller log something the database has never
/// heard of.
/// </summary>
public record LogMealRequest
{
    public Guid? FoodId { get; init; }
    public Guid? RecipeId { get; init; }
    public DateOnly? EntryDate { get; init; }

    /// <summary>
    /// When the meal was eaten. Lets a caller backfill at a specific time;
    /// stored as UTC, defaults to now.
    /// </summary>
    public DateTime? LoggedAt { get; init; }

    public string MealType { get; init; } = "SNACK";
    public decimal Servings { get; init; } = 1;
    /// <summary>
    /// Total consumed weight for a custom meal. Foods use their serving size;
    /// recipes use their ingredient weights.
    /// </summary>
    public decimal? AmountGrams { get; init; }
    public decimal? Calories { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// How many people shared this meal, 2 to 10. The totals and amount sent are
    /// the whole meal's; the API logs one share and labels the entry, so no
    /// client divides anything itself.
    /// </summary>
    public int? SharedBetween { get; init; }

    /// <summary>
    /// Nutrition per 100 g of a custom meal. With <see cref="AmountGrams"/>, the
    /// server computes the totals from these and ignores any totals sent
    /// alongside, so every client logs the same numbers for the same food.
    /// </summary>
    public NutritionPer100g? Per100g { get; init; }
}

/// <summary>
/// Body of POST /api/Nutrition/log: a catalogue food or a recipe, by what the person typed. A food
/// takes <c>AmountGrams</c>, the weight eaten, or <c>Servings</c> of its serving size; a recipe
/// takes <c>Servings</c>. The server turns grams into servings, so no client converts anything.
/// </summary>
public record LogFoodRequest
{
    public Guid? FoodId { get; init; }
    public Guid? RecipeId { get; init; }

    /// <summary>The diary day. Left out, it is today in the user's time zone, the day the streak counts.</summary>
    public DateOnly? EntryDate { get; init; }
    public string MealType { get; init; } = "SNACK";
    public decimal Servings { get; init; } = 1;

    /// <summary>The weight of a food eaten, in grams. When set, it wins over <c>Servings</c>.</summary>
    public decimal? AmountGrams { get; init; }
}

/// <summary>Nutrition for 100 g of a food, as the AI proposal reports it and as a client logs it.</summary>
public record NutritionPer100g
{
    public decimal Calories { get; init; }
    public decimal ProteinGrams { get; init; }
    public decimal CarbsGrams { get; init; }
    public decimal FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
}

/// <summary>
/// Body of POST /api/Meals/preview: the meal form's lines as the person has typed them so far.
/// Nothing is saved; the server answers what each line and the meal come to, by the same rules
/// logging uses, so the form shows numbers and computes none.
/// </summary>
public record MealPreviewRequest
{
    public List<MealPreviewLine> Lines { get; init; } = [];
}

/// <summary>
/// One line of the meal form, named by the caller's <c>Key</c>: a catalogue food with
/// <c>Grams</c>, a recipe with <c>Servings</c>, an estimate with <c>Per100g</c> and
/// <c>Grams</c>, or a custom line with the <c>Calories</c> and <c>ProteinGrams</c> typed.
/// </summary>
public record MealPreviewLine
{
    public string Key { get; init; } = string.Empty;
    public Guid? FoodId { get; init; }
    public Guid? RecipeId { get; init; }
    public decimal? Grams { get; init; }
    public decimal? Servings { get; init; }
    public NutritionPer100g? Per100g { get; init; }
    public decimal? Calories { get; init; }
    public decimal? ProteinGrams { get; init; }
}

/// <summary>What one line comes to. Null when it cannot be known (a recipe without complete nutrition, a food the person cannot see).</summary>
public record MealPreviewLineResult(string Key, decimal? Calories, decimal? ProteinGrams);

/// <summary>Response of POST /api/Meals/preview. <c>Complete</c> is false when a line's nutrition is unknown, so the total is a floor.</summary>
public record MealPreviewResult(List<MealPreviewLineResult> Lines, decimal Calories, decimal ProteinGrams, bool Complete);
