using Mizan.Domain.Planning;

namespace Mizan.Domain.Entities;

/// <summary>
/// One thing to eat on one day of a plan: a recipe with servings, a catalogue
/// food with a weight, or a plain line. Replaces the recipe-only plan entry.
/// </summary>
public class PlanEntry
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }

    /// <summary>0 is the first day of the plan.</summary>
    public int DayIndex { get; set; }

    /// <summary>A <see cref="Constants.MealTypes"/> value: BREAKFAST, LUNCH, DINNER or SNACK.</summary>
    public string Slot { get; set; } = Constants.MealTypes.Dinner;

    public PlanEntryKind Kind { get; set; } = PlanEntryKind.Recipe;

    public Guid? RecipeId { get; set; }
    public decimal? Servings { get; set; }

    public Guid? FoodId { get; set; }
    public decimal? Grams { get; set; }

    /// <summary>The line itself for a text entry. For other kinds, a display name may be kept here.</summary>
    public string? Text { get; set; }

    public string? Notes { get; set; }
    public int SortOrder { get; set; }

    public Guid? PrepSessionId { get; set; }

    // Navigation properties
    public virtual MealPlan MealPlan { get; set; } = null!;
    public virtual Recipe? Recipe { get; set; }
    public virtual Food? Food { get; set; }
    public virtual PrepSession? PrepSession { get; set; }
}
