namespace Mizan.Domain.Entities;

public class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid? FoodId { get; set; }
    public Guid? SubRecipeId { get; set; }
    public string IngredientText { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Unit { get; set; }
    public int SortOrder { get; set; }

    // Navigation properties
    public virtual Recipe Recipe { get; set; } = null!;
    public virtual Food? Food { get; set; }
    public virtual Recipe? SubRecipe { get; set; }
}
