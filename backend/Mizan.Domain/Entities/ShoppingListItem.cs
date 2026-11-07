namespace Mizan.Domain.Entities;

public class ShoppingListItem
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public Guid? FoodId { get; set; }
    public string ItemName { get; set; } = string.Empty;

    /// <summary>The amount as written, with <see cref="Unit"/>, when the weight is not known.</summary>
    public decimal? Amount { get; set; }
    public string? Unit { get; set; }

    /// <summary>The weight to buy, when the aggregator could convert it.</summary>
    public decimal? Grams { get; set; }

    public string? Category { get; set; }

    /// <summary>Set when a generated line could not be converted or came from a plain text entry.</summary>
    public bool NeedsReview { get; set; }

    public Guid? PrepSessionId { get; set; }
    public int SortOrder { get; set; }
    public bool IsChecked { get; set; }

    // Navigation properties
    public virtual ShoppingList ShoppingList { get; set; } = null!;
    public virtual Food? Food { get; set; }
}
