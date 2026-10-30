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

/// <summary>One line of a list. <c>Cost</c> is its grams at the region's price; null when the line is unpriced, and <c>UnpricedReason</c> says why.</summary>
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

    /// <summary>The confidence of the price behind <see cref="Cost"/>: Observed, Reported or Estimated.</summary>
    public string? CostConfidence { get; init; }
    public string? UnpricedReason { get; init; }
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

    /// <summary>The region's currency the costs are in.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>The priced lines added up; null when nothing is priced.</summary>
    public decimal? TotalCost { get; init; }

    /// <summary>Share of lines that carry a price, 0 to 1.</summary>
    public decimal CostCoverage { get; init; }

    /// <summary>The lowest confidence among the prices used; null when nothing is priced.</summary>
    public string? CostConfidence { get; init; }
    public int UnpricedCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record ShoppingListCreatedResult(Guid Id);

public record ShoppingListItemCreatedResult(Guid Id);

public record ClearCheckedResult(int Removed);
