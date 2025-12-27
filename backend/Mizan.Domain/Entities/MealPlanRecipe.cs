namespace Mizan.Domain.Entities;

public class MealPlanRecipe
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }
    public Guid RecipeId { get; set; }
    public DateOnly Date { get; set; }
    public string MealType { get; set; } = "dinner"; // breakfast, lunch, dinner, snack
    public decimal Servings { get; set; } = 1;

    // Navigation properties
    public virtual MealPlan MealPlan { get; set; } = null!;
    public virtual Recipe Recipe { get; set; } = null!;
}
