namespace Mizan.Contracts.Meals;

/// <summary>
/// One thing eaten, in a batch log: exactly one of a food, a recipe, a meal plan entry, or a
/// name with the nutrition eaten (totals, or per-100 g figures with <see cref="AmountGrams"/>).
/// </summary>
public sealed record LogFoodItem
{
    public Guid? FoodId { get; init; }
    public Guid? RecipeId { get; init; }

    /// <summary>A meal plan entry, with <see cref="PlanId"/>. Logged with the nutrition the plan computed.</summary>
    public Guid? PlanEntryId { get; init; }
    public Guid? PlanId { get; init; }

    public string? Name { get; init; }

    /// <summary>A count of the food's serving or the recipe's servings, not grams. 1 by default.</summary>
    public decimal? Servings { get; init; }

    /// <summary>Overrides the batch's meal type for this item.</summary>
    public string? MealType { get; init; }

    /// <summary>Overrides the batch's date for this item.</summary>
    public DateOnly? Date { get; init; }

    public DateTime? LoggedAt { get; init; }
    public decimal? AmountGrams { get; init; }
    public decimal? Calories { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
    public decimal? CaloriesPer100g { get; init; }
    public decimal? ProteinPer100g { get; init; }
    public decimal? CarbsPer100g { get; init; }
    public decimal? FatPer100g { get; init; }
    public decimal? FiberPer100g { get; init; }
}

/// <summary>
/// What a batch log came to. The items are written one after another and are not one
/// transaction: <see cref="Completed"/> are saved, <see cref="Failed"/> says what stopped the
/// batch, and <see cref="Remaining"/> lists the indexes still to send, so a retry repeats only those.
/// </summary>
public sealed record LogFoodReceipt(
    bool Complete,
    IReadOnlyList<LoggedFoodItem> Completed,
    FailedFoodItem? Failed,
    IReadOnlyList<int> Remaining,
    string? Guidance);

/// <summary>One item that was saved: the diary rows written, and the group a recipe's rows share.</summary>
public sealed record LoggedFoodItem(
    int Index,
    string Kind,
    DateOnly? Date,
    string? MealType,
    IReadOnlyList<Guid> EntryIds,
    Guid? GroupId,
    IReadOnlyList<string> Warnings);

/// <summary>The item that stopped the batch. When <see cref="MayHaveBeenSaved"/>, check the diary before sending it again.</summary>
public sealed record FailedFoodItem(int Index, string Kind, string Error, bool MayHaveBeenSaved);
