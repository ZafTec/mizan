using System.ComponentModel;
using Mizan.Contracts.Planning;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

/// <summary>
/// Shopping lists and their lines. A list generated from a plan
/// (generate_shopping_list) is an ordinary list once it exists, so every tool
/// here applies to it too.
/// </summary>
[McpServerToolType]
public sealed class ShoppingListTools
{
    private readonly IBackendApiClient _api;

    public ShoppingListTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_shopping_lists", ReadOnly = true, Idempotent = true)]
    [Description("List the user's shopping lists with item, checked and needs-review counts, and the plan each was generated from.")]
    public async Task<string> ListShoppingLists(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/ShoppingLists?page={page}&pageSize={pageSize}", ct);
    }

    [McpServerTool(Name = "get_shopping_list", ReadOnly = true, Idempotent = true)]
    [Description("Get a shopping list with its items: amount or grams, category, checked state, needsReview, and the prep session each belongs to. Items are ordered by prep session, then sort order.")]
    public async Task<string> GetShoppingList(
        [Description("Shopping list UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/ShoppingLists/{ToolArguments.ParseId(id, "id")}", ct);
    }

    [McpServerTool(Name = "create_shopping_list", Idempotent = false)]
    [Description("Create an empty shopping list. To build one from a meal plan, use generate_shopping_list instead.")]
    public async Task<string> CreateShoppingList(
        [Description("List name")] string name,
        [Description("Household UUID to share with (optional)")] string? householdId = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/ShoppingLists", new CreateShoppingListRequest(name, ToolArguments.ParseOptionalId(householdId, "householdId")), ct);
    }

    [McpServerTool(Name = "update_shopping_list", Idempotent = true)]
    [Description("Rename a shopping list.")]
    public async Task<string> UpdateShoppingList(
        [Description("Shopping list UUID")] string id,
        [Description("New name")] string name,
        CancellationToken ct = default)
    {
        return await _api.PutAsync($"/api/ShoppingLists/{ToolArguments.ParseId(id, "id")}", new UpdateShoppingListRequest(name), ct);
    }

    [McpServerTool(Name = "delete_shopping_list", Destructive = true)]
    [Description("Delete a shopping list and all its items. This is permanent; confirm with the user first.")]
    public async Task<string> DeleteShoppingList(
        [Description("Shopping list UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/ShoppingLists/{ToolArguments.ParseId(id, "id")}", ct);
    }

    [McpServerTool(Name = "add_shopping_list_item", Idempotent = false)]
    [Description("Add an item to a shopping list. Give grams when the weight is known, otherwise amount and unit. foodId links the line to a catalogue food; prepSessionId files it under a plan's cooking session.")]
    public async Task<string> AddItem(
        [Description("Shopping list UUID")] string listId,
        [Description("Item name")] string itemName,
        [Description("Amount, when not weighed (optional)")] decimal? amount = null,
        [Description("Unit for the amount, e.g. pcs, bunch, cups (optional)")] string? unit = null,
        [Description("Category e.g. produce, dairy (optional)")] string? category = null,
        [Description("Weight in grams, when known (optional)")] decimal? grams = null,
        [Description("Catalogue food UUID the line is for (optional)")] string? foodId = null,
        [Description("Prep session UUID the line belongs to (optional)")] string? prepSessionId = null,
        CancellationToken ct = default)
    {
        var body = new AddShoppingListItemRequest(itemName, amount, unit, category, grams,
            ToolArguments.ParseOptionalId(foodId, "foodId"), ToolArguments.ParseOptionalId(prepSessionId, "prepSessionId"));
        return await _api.PostAsync($"/api/ShoppingLists/{ToolArguments.ParseId(listId, "listId")}/items", body, ct);
    }

    [McpServerTool(Name = "update_shopping_list_item", Idempotent = true)]
    [Description("Change an item. Only the arguments passed change. Clear needsReview once the amount has been checked.")]
    public async Task<string> UpdateItem(
        [Description("Item UUID")] string itemId,
        [Description("New item name (optional)")] string? itemName = null,
        [Description("New amount (optional)")] decimal? amount = null,
        [Description("New unit (optional)")] string? unit = null,
        [Description("New weight in grams (optional)")] decimal? grams = null,
        [Description("New category (optional)")] string? category = null,
        [Description("Checked (bought) or not (optional)")] bool? isChecked = null,
        [Description("Whether the line still needs a person to check its amount (optional)")] bool? needsReview = null,
        [Description("Position in the list (optional)")] int? sortOrder = null,
        CancellationToken ct = default)
    {
        var body = new UpdateShoppingListItemRequest
        {
            ItemName = itemName,
            Amount = amount,
            Unit = unit,
            Grams = grams,
            Category = category,
            IsChecked = isChecked,
            NeedsReview = needsReview,
            SortOrder = sortOrder,
        };
        return await _api.PutAsync($"/api/ShoppingLists/items/{ToolArguments.ParseId(itemId, "itemId")}", body, ct);
    }

    [McpServerTool(Name = "delete_shopping_list_item", Destructive = true)]
    [Description("Remove one item from its shopping list.")]
    public async Task<string> DeleteItem(
        [Description("Item UUID")] string itemId,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/ShoppingLists/items/{ToolArguments.ParseId(itemId, "itemId")}", ct);
    }

    [McpServerTool(Name = "toggle_shopping_list_item", Idempotent = true)]
    [Description("Mark a shopping list item bought (checked) or not.")]
    public async Task<string> ToggleItem(
        [Description("Item UUID")] string itemId,
        [Description("Check or uncheck the item")] bool isChecked,
        CancellationToken ct = default)
    {
        return await _api.PatchAsync($"/api/ShoppingLists/items/{ToolArguments.ParseId(itemId, "itemId")}/toggle", new ToggleShoppingListItemRequest(isChecked), ct);
    }

    [McpServerTool(Name = "clear_checked_items", Destructive = true)]
    [Description("Remove every checked (bought) item from a shopping list. Returns how many were removed.")]
    public async Task<string> ClearChecked(
        [Description("Shopping list UUID")] string listId,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/ShoppingLists/{ToolArguments.ParseId(listId, "listId")}/clear-checked", null, ct);
    }
}
