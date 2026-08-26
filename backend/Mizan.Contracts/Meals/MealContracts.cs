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
    public decimal? Calories { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
    public string Name { get; init; } = string.Empty;
}
