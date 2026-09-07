namespace Mizan.Contracts.Recipes;

/// <summary>Promote a logged meal, retaining quantities and explicit missing weights.</summary>
public record PromoteMealToRecipeRequest(
    DateOnly EntryDate,
    string MealType,
    string Title,
    Guid? HouseholdId = null,
    Dictionary<Guid, decimal>? RecipeYields = null,
    Dictionary<Guid, decimal>? EntryWeightsGrams = null);

/// <summary>
/// One line of a recipe. FoodId links it to the database so the recipe's
/// nutrition can be computed; IngredientText alone means "a pinch of salt" and
/// contributes nothing to the totals.
/// </summary>
public record CreateRecipeIngredientDto
{
    public Guid? FoodId { get; init; }
    public string IngredientText { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string? Unit { get; init; }
}

/// <summary>Body of POST /api/Recipes.</summary>
public record CreateRecipeRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>Free text. Was an ordered table; see docs/ARCHITECTURE.md#navigation-and-logging.</summary>
    public string? Instructions { get; init; }

    public int Servings { get; init; } = 1;
    public int? PrepTimeMinutes { get; init; }
    public int? CookTimeMinutes { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsPublic { get; init; }
    public Guid? HouseholdId { get; init; }
    public List<CreateRecipeIngredientDto> Ingredients { get; init; } = new();
}

/// <summary>Body of PUT /api/Recipes/{id}.</summary>
public record UpdateRecipeRequest
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>Free text. Was an ordered table; see docs/ARCHITECTURE.md#navigation-and-logging.</summary>
    public string? Instructions { get; init; }

    public int Servings { get; init; } = 1;
    public int? PrepTimeMinutes { get; init; }
    public int? CookTimeMinutes { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsPublic { get; init; }
    public List<CreateRecipeIngredientDto> Ingredients { get; init; } = new();
}
