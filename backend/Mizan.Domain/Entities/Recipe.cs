namespace Mizan.Domain.Entities;

public class Recipe
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Servings { get; set; } = 1;
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Household? Household { get; set; }
    public virtual ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public virtual ICollection<RecipeInstruction> Instructions { get; set; } = new List<RecipeInstruction>();
    public virtual RecipeNutrition? Nutrition { get; set; }
    public virtual ICollection<RecipeTag> Tags { get; set; } = new List<RecipeTag>();
    public virtual ICollection<MealPlanRecipe> MealPlanRecipes { get; set; } = new List<MealPlanRecipe>();
    public virtual ICollection<FoodDiaryEntry> DiaryEntries { get; set; } = new List<FoodDiaryEntry>();
}
