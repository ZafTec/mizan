namespace Mizan.Domain.Entities;

public class ShoppingList
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string? Name { get; set; }

    /// <summary>The plan this list was generated from, when it was.</summary>
    public Guid? MealPlanId { get; set; }
    public DateTime? GeneratedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Household? Household { get; set; }
    public virtual MealPlan? MealPlan { get; set; }
    public virtual ICollection<ShoppingListItem> Items { get; set; } = new List<ShoppingListItem>();
}
