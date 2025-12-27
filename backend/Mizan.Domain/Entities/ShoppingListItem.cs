namespace Mizan.Domain.Entities;

public class ShoppingListItem
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public Guid? FoodId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public bool IsChecked { get; set; }

    // Navigation properties
    public virtual ShoppingList ShoppingList { get; set; } = null!;
    public virtual Food? Food { get; set; }
}
