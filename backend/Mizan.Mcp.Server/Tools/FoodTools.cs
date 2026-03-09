using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class FoodTools
{
    private readonly IBackendApiClient _api;

    public FoodTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "search_foods", ReadOnly = true, Idempotent = true)]
    [Description("Search for food ingredients in the database. Returns paginated results with nutritional info per 100g.")]
    public async Task<string> SearchFoods(
        [Description("Search term (name or brand)")] string? search = null,
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20, max 100)")] int pageSize = 20,
        [Description("Sort by: name, calories, protein, verified")] string? sortBy = null,
        [Description("Sort direction: asc or desc")] string? sortOrder = null,
        CancellationToken ct = default)
    {
        var qs = $"/api/Foods/search?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) qs += $"&searchTerm={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(sortBy)) qs += $"&sortBy={sortBy}";
        if (!string.IsNullOrEmpty(sortOrder)) qs += $"&sortOrder={sortOrder}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "get_food", ReadOnly = true, Idempotent = true)]
    [Description("Get a single food ingredient by its ID, including full nutritional data.")]
    public async Task<string> GetFood(
        [Description("Food UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Foods/{id}", ct);
    }

    [McpServerTool(Name = "create_food")]
    [Description("Create a new food ingredient (requires admin role). Provide nutritional values per 100g.")]
    public async Task<string> CreateFood(
        [Description("Food name")] string name,
        [Description("Calories per 100g")] int caloriesPer100g,
        [Description("Protein grams per 100g")] decimal proteinPer100g,
        [Description("Carbs grams per 100g")] decimal carbsPer100g,
        [Description("Fat grams per 100g")] decimal fatPer100g,
        [Description("Brand name")] string? brand = null,
        [Description("Barcode (EAN/UPC)")] string? barcode = null,
        [Description("Fiber grams per 100g")] decimal? fiberPer100g = null,
        [Description("Sugar grams per 100g")] decimal? sugarPer100g = null,
        [Description("Sodium mg per 100g")] decimal? sodiumPer100g = null,
        [Description("Serving size in servingUnit (default 100)")] decimal? servingSize = null,
        [Description("Serving unit, e.g. 'g', 'ml', 'oz' (default 'g')")] string? servingUnit = null,
        [Description("Mark as verified (default false)")] bool isVerified = false,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Foods", new
        {
            name, brand, barcode, caloriesPer100g, proteinPer100g, carbsPer100g, fatPer100g,
            fiberPer100g, sugarPer100g, sodiumPer100g,
            servingSize = servingSize ?? 100m, servingUnit = servingUnit ?? "g", isVerified
        }, ct);
    }

    [McpServerTool(Name = "update_food")]
    [Description("Update an existing food ingredient (requires admin role).")]
    public async Task<string> UpdateFood(
        [Description("Food UUID")] string id,
        [Description("Food name")] string name,
        [Description("Calories per 100g")] int caloriesPer100g,
        [Description("Protein grams per 100g")] decimal proteinPer100g,
        [Description("Carbs grams per 100g")] decimal carbsPer100g,
        [Description("Fat grams per 100g")] decimal fatPer100g,
        [Description("Brand name")] string? brand = null,
        [Description("Barcode (EAN/UPC)")] string? barcode = null,
        [Description("Fiber grams per 100g")] decimal? fiberPer100g = null,
        [Description("Sugar grams per 100g")] decimal? sugarPer100g = null,
        [Description("Sodium mg per 100g")] decimal? sodiumPer100g = null,
        [Description("Serving size in servingUnit (default 100)")] decimal? servingSize = null,
        [Description("Serving unit, e.g. 'g', 'ml', 'oz' (default 'g')")] string? servingUnit = null,
        [Description("Mark as verified (default false)")] bool isVerified = false,
        CancellationToken ct = default)
    {
        return await _api.PutAsync($"/api/Foods/{id}", new
        {
            id, name, brand, barcode, caloriesPer100g, proteinPer100g, carbsPer100g, fatPer100g,
            fiberPer100g, sugarPer100g, sodiumPer100g,
            servingSize = servingSize ?? 100m, servingUnit = servingUnit ?? "g", isVerified
        }, ct);
    }

    [McpServerTool(Name = "delete_food", Destructive = true)]
    [Description("Delete a food ingredient (requires admin role). This is permanent.")]
    public async Task<string> DeleteFood(
        [Description("Food UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/Foods/{id}", ct);
    }
}
