
namespace Mizan.Application.Commands;

public record CreateRecipeIngredientDto
{
    public Guid? FoodId { get; init; }
    public string IngredientText { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string? Unit { get; init; }
}

public record CreateRecipeNutritionDto
{
    public decimal? CaloriesPerServing { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
}
