namespace Mizan.Domain.Entities;

public class FoodDiaryEntry : IChangeTracked
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? FoodId { get; set; }
    public Guid? RecipeId { get; set; }

    /// <summary>The plan entry this row was logged from, so adherence can be measured.</summary>
    public Guid? PlanEntryId { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public decimal? AmountGrams { get; set; }
    public DateOnly EntryDate { get; set; }
    public string MealType { get; set; } = "snack"; // breakfast, lunch, dinner, snack
    public decimal Servings { get; set; } = 1;
    public decimal? Calories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public decimal? FiberGrams { get; set; }
    public decimal? ProteinCalorieRatio { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Food? Food { get; set; }
    public virtual Recipe? Recipe { get; set; }
    public virtual PlanEntry? PlanEntry { get; set; }
}
