using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class ShoppingListTools
{
    private readonly IBackendApiClient _api;

    public ShoppingListTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_shopping_lists", ReadOnly = true, Idempotent = true)]
    [Description("List the user's shopping lists with item counts.")]
    public async Task<string> ListShoppingLists(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/ShoppingLists?page={page}&pageSize={pageSize}", ct);
    }

    [McpServerTool(Name = "get_shopping_list", ReadOnly = true, Idempotent = true)]
    [Description("Get a shopping list with all its items, including checked/unchecked status.")]
    public async Task<string> GetShoppingList(
        [Description("Shopping list UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/ShoppingLists/{id}", ct);
    }

    [McpServerTool(Name = "create_shopping_list")]
    [Description("Create a new shopping list.")]
    public async Task<string> CreateShoppingList(
        [Description("List name")] string name,
        [Description("Household UUID to share with (optional)")] string? householdId = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/ShoppingLists", new { name, householdId }, ct);
    }

    [McpServerTool(Name = "add_shopping_list_item")]
    [Description("Add an item to a shopping list.")]
    public async Task<string> AddItem(
        [Description("Shopping list UUID")] string listId,
        [Description("Item name")] string itemName,
        [Description("Amount (optional)")] decimal? amount = null,
        [Description("Unit e.g. kg, g, pcs (optional)")] string? unit = null,
        [Description("Category e.g. produce, dairy (optional)")] string? category = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/ShoppingLists/{listId}/items", new
        {
            itemName, amount, unit, category
        }, ct);
    }

    [McpServerTool(Name = "toggle_shopping_list_item")]
    [Description("Toggle a shopping list item as checked/unchecked (bought/not bought).")]
    public async Task<string> ToggleItem(
        [Description("Item UUID")] string itemId,
        [Description("Check or uncheck the item")] bool isChecked,
        CancellationToken ct = default)
    {
        return await _api.PatchAsync($"/api/ShoppingLists/items/{itemId}/toggle", new { isChecked }, ct);
    }
}
