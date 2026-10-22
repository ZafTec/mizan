namespace Mizan.Domain.Entities;

/// <summary>
/// A cooking session in a plan that cooks in batch. Entries point at the
/// session that makes them, and the shopping list groups by session.
/// </summary>
public class PrepSession
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }

    /// <summary>The day the cooking happens, counted from the first day of the plan.</summary>
    public int DayIndex { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // Navigation properties
    public virtual MealPlan MealPlan { get; set; } = null!;
    public virtual ICollection<PlanEntry> Entries { get; set; } = new List<PlanEntry>();
}
