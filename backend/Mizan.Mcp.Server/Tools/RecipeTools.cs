using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class RecipeTools
{
    private readonly IBackendApiClient _api;

    public RecipeTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "search_recipes", ReadOnly = true, Idempotent = true)]
    [Description("Search for recipes. Returns paginated list with title, macros, prep/cook time, tags.")]
    public async Task<string> SearchRecipes(
        [Description("Search term")] string? search = null,
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        [Description("Sort by: title, createdat")] string? sortBy = null,
        [Description("Sort direction: asc or desc")] string? sortOrder = null,
        [Description("Comma-separated tags to filter by")] string? tags = null,
        [Description("Only return user's favorite recipes")] bool favoritesOnly = false,
        CancellationToken ct = default)
    {
        var qs = $"/api/Recipes?page={page}&pageSize={pageSize}&favoritesOnly={favoritesOnly}";
        if (!string.IsNullOrWhiteSpace(search)) qs += $"&searchTerm={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(sortBy)) qs += $"&sortBy={sortBy}";
        if (!string.IsNullOrEmpty(sortOrder)) qs += $"&sortOrder={sortOrder}";
        if (!string.IsNullOrEmpty(tags))
        {
            foreach (var tag in tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                qs += $"&tags={Uri.EscapeDataString(tag)}";
        }
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "get_recipe", ReadOnly = true, Idempotent = true)]
    [Description("Get full recipe details including ingredients, instructions, and nutritional breakdown.")]
    public async Task<string> GetRecipe(
        [Description("Recipe UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Recipes/{id}", ct);
    }

    [McpServerTool(Name = "create_recipe")]
    [Description("Create a new recipe. The recipe will be public by default.")]
    public async Task<string> CreateRecipe(
        [Description("Recipe title")] string title,
        [Description("Recipe description")] string? description = null,
        [Description("Number of servings")] int? servings = null,
        [Description("Prep time in minutes")] int? prepTimeMinutes = null,
        [Description("Cook time in minutes")] int? cookTimeMinutes = null,
        [Description("Make recipe public (default true)")] bool isPublic = true,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Recipes", new
        {
            title, description, servings, prepTimeMinutes, cookTimeMinutes, isPublic
        }, ct);
    }

    [McpServerTool(Name = "update_recipe")]
    [Description("Update an existing recipe. Only the recipe owner can update.")]
    public async Task<string> UpdateRecipe(
        [Description("Recipe UUID")] string id,
        [Description("Recipe title")] string title,
        [Description("Recipe description")] string? description = null,
        [Description("Number of servings")] int? servings = null,
        [Description("Prep time in minutes")] int? prepTimeMinutes = null,
        [Description("Cook time in minutes")] int? cookTimeMinutes = null,
        [Description("Make recipe public")] bool? isPublic = null,
        CancellationToken ct = default)
    {
        return await _api.PutAsync($"/api/Recipes/{id}", new
        {
            id, title, description, servings, prepTimeMinutes, cookTimeMinutes, isPublic
        }, ct);
    }

    [McpServerTool(Name = "delete_recipe", Destructive = true)]
    [Description("Delete a recipe. Only the recipe owner can delete. This is permanent.")]
    public async Task<string> DeleteRecipe(
        [Description("Recipe UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/Recipes/{id}", ct);
    }

    [McpServerTool(Name = "toggle_favorite_recipe")]
    [Description("Toggle a recipe as favorite/unfavorite for the current user.")]
    public async Task<string> ToggleFavorite(
        [Description("Recipe UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/Recipes/{id}/favorite", null, ct);
    }
}
