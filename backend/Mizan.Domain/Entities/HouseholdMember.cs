namespace Mizan.Domain.Entities;

public class HouseholdMember
{
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "member"; // owner, admin, member
    public bool CanEditRecipes { get; set; } = true;
    public bool CanEditShoppingList { get; set; } = true;
    public bool CanViewNutrition { get; set; } = false;
    public DateTime JoinedAt { get; set; }

    // Navigation properties
    public virtual Household Household { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
