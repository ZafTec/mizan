namespace Mizan.Domain.Entities;

public class RecipeNutrition
{
    public Guid RecipeId { get; set; }
    public decimal? CaloriesPerServing { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public decimal? FiberGrams { get; set; }
    public decimal? SugarGrams { get; set; }
    public decimal? SodiumMg { get; set; }

    // Navigation property
    public virtual Recipe Recipe { get; set; } = null!;
}
