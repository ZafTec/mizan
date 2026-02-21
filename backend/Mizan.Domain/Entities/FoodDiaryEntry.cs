namespace Mizan.Domain.Entities;

public class FoodDiaryEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? FoodId { get; set; }
    public Guid? RecipeId { get; set; }
    public DateOnly EntryDate { get; set; }
    public string MealType { get; set; } = "snack"; // breakfast, lunch, dinner, snack
    public decimal Servings { get; set; } = 1;
    public decimal? Calories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public decimal? FiberGrams { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Food? Food { get; set; }
    public virtual Recipe? Recipe { get; set; }
}
