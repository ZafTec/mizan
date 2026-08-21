
namespace Mizan.Application.Commands;

public record CreateRecipeIngredientDto
{
    public Guid? FoodId { get; init; }
    public string IngredientText { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string? Unit { get; init; }
}
