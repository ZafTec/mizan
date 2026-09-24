using Mizan.Domain.Recipes;

namespace Mizan.Domain.Entities;

/// <summary>
/// Per-serving nutrition kept from before Mizan calculated it from
/// ingredients - today only the values v1 stored for imported recipes.
///
/// It stands in for a calculation that cannot be done (a measured line with no
/// linked food) and never overrides one that can. It describes one exact set of
/// ingredient lines and one serving count: <see cref="Describes"/> is false the
/// moment either changes, so an edit retires it without any code having to
/// remember to.
/// </summary>
public class RecipeNutritionSnapshot
{
    public const string LegacySource = "legacy_v1";

    public Guid RecipeId { get; set; }
    public decimal Calories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatGrams { get; set; }
    public decimal? FiberGrams { get; set; }

    /// <summary>The recipe's serving count when the values were taken.</summary>
    public int Servings { get; set; }

    /// <summary><see cref="RecipeFingerprint.Of"/> of the ingredient lines the values describe.</summary>
    public string IngredientsFingerprint { get; set; } = string.Empty;
    public string Source { get; set; } = LegacySource;
    public DateTime CapturedAt { get; set; }

    public bool Describes(IEnumerable<RecipeIngredient> ingredients, int servings) =>
        servings == Servings && RecipeFingerprint.Of(ingredients) == IngredientsFingerprint;

    public RecipeNutritionTotals PerServing =>
        new(Calories, ProteinGrams, CarbsGrams, FatGrams, FiberGrams ?? 0m);
}
