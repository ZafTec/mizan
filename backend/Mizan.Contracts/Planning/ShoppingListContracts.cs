namespace Mizan.Contracts.Planning;

/// <summary>Body of POST /api/ShoppingLists.</summary>
public record CreateShoppingListRequest(string Name, Guid? HouseholdId = null);

/// <summary>Body of PUT /api/ShoppingLists/{id}.</summary>
public record UpdateShoppingListRequest(string Name);

/// <summary>Body of POST /api/ShoppingLists/{id}/items. <c>Grams</c> is the weight when it is known; <c>Amount</c> and <c>Unit</c> otherwise.</summary>
public record AddShoppingListItemRequest(
    string ItemName,
    decimal? Amount = null,
    string? Unit = null,
    string? Category = null,
    decimal? Grams = null,
    Guid? FoodId = null,
    Guid? PrepSessionId = null);

/// <summary>Body of PUT /api/ShoppingLists/items/{itemId}. Only the fields that are sent change.</summary>
public record UpdateShoppingListItemRequest
{
    public string? ItemName { get; init; }
    public decimal? Amount { get; init; }
    public string? Unit { get; init; }
    public decimal? Grams { get; init; }
    public string? Category { get; init; }
    public bool? IsChecked { get; init; }
    public bool? NeedsReview { get; init; }
    public int? SortOrder { get; init; }
}

/// <summary>Body of PATCH /api/ShoppingLists/items/{itemId}/toggle.</summary>
public record ToggleShoppingListItemRequest(bool IsChecked);

/// <summary>A row of GET /api/ShoppingLists.</summary>
public record ShoppingListSummaryDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public Guid UserId { get; init; }
    public Guid? HouseholdId { get; init; }
    public Guid? MealPlanId { get; init; }
    public DateTime? GeneratedAt { get; init; }
    public int TotalItems { get; init; }
    public int CheckedItems { get; init; }
    public int NeedsReviewCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>One line of a list. <c>Cost</c> is reserved for the prices step and null for now.</summary>
public record ShoppingListItemDto
{
    public Guid Id { get; init; }
    public Guid? FoodId { get; init; }
    public string? FoodName { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string? Unit { get; init; }
    public decimal? Grams { get; init; }
    public string? Category { get; init; }
    public bool NeedsReview { get; init; }
    public Guid? PrepSessionId { get; init; }
    public string? PrepSessionTitle { get; init; }
    public int SortOrder { get; init; }
    public bool IsChecked { get; init; }
    public decimal? Cost { get; init; }
}

/// <summary>GET /api/ShoppingLists/{id}. Items are ordered by prep session, then by their sort order.</summary>
public record ShoppingListDetailDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public Guid UserId { get; init; }
    public Guid? HouseholdId { get; init; }
    public Guid? MealPlanId { get; init; }
    public string? MealPlanName { get; init; }
    public DateTime? GeneratedAt { get; init; }
    public List<ShoppingListItemDto> Items { get; init; } = [];
    public int TotalItems { get; init; }
    public int CheckedItems { get; init; }
    public int NeedsReviewCount { get; init; }

    /// <summary>Whether the viewer may change the list: its owner, or a household member allowed to edit lists.</summary>
    public bool CanEdit { get; init; }
    public decimal? TotalCost { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record ShoppingListCreatedResult(Guid Id);

public record ShoppingListItemCreatedResult(Guid Id);

public record ClearCheckedResult(int Removed);
