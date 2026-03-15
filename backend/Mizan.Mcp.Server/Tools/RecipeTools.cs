using System.ComponentModel;
using System.Text.Json;
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
    [Description("Create a new recipe with ingredients. Use search_foods first to get foodId UUIDs for accurate nutrition calculation.")]
    public async Task<string> CreateRecipe(
        [Description("Recipe title")] string title,
        [Description("JSON array of ingredients. Each object MUST have 'ingredientText' (human-readable, e.g. '200g chicken breast'). Optional: 'foodId' (UUID from search_foods for nutrition calc), 'subRecipeId' (UUID of another recipe to nest), 'amount' (grams, or servings for sub-recipes), 'unit'. Example: [{\"ingredientText\":\"200g chicken breast\",\"foodId\":\"uuid\",\"amount\":200,\"unit\":\"g\"},{\"ingredientText\":\"1 cup rice\"}]")] string ingredientsJson,
        [Description("Recipe description")] string? description = null,
        [Description("Number of servings (default 1)")] int? servings = null,
        [Description("Prep time in minutes")] int? prepTimeMinutes = null,
        [Description("Cook time in minutes")] int? cookTimeMinutes = null,
        [Description("JSON array of instruction step strings, e.g. [\"Preheat oven\",\"Mix ingredients\"]")] string? instructionsJson = null,
        [Description("Comma-separated tags, e.g. 'breakfast,healthy,quick'")] string? tags = null,
        [Description("Make recipe public (default true)")] bool isPublic = true,
        [Description("Image URL for the recipe")] string? imageUrl = null,
        [Description("Household UUID to share recipe with a household")] string? householdId = null,
        [Description("Manual nutrition JSON (only used when no ingredients have foodId). Object with: caloriesPerServing, proteinGrams, carbsGrams, fatGrams, fiberGrams")] string? nutritionJson = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("'title' is required.");
        if (string.IsNullOrWhiteSpace(ingredientsJson))
            throw new ArgumentException("'ingredientsJson' is required.");

        var ingredients = ParseIngredients(ingredientsJson);
        var instructions = ParseStringArray(instructionsJson);
        var tagList = ParseTags(tags);
        var nutrition = nutritionJson != null ? JsonSerializer.Deserialize<object>(nutritionJson) : null;

        return await _api.PostAsync("/api/Recipes", new
        {
            title, description, servings, prepTimeMinutes, cookTimeMinutes,
            imageUrl, isPublic, householdId,
            ingredients, instructions, tags = tagList, nutrition
        }, ct);
    }

    [McpServerTool(Name = "update_recipe")]
    [Description("Update an existing recipe. Only the recipe owner can update. All ingredients/instructions/tags are fully replaced.")]
    public async Task<string> UpdateRecipe(
        [Description("Recipe UUID")] string id,
        [Description("Recipe title")] string title,
        [Description("JSON array of ingredients. Each object MUST have 'ingredientText' (human-readable). Optional: 'foodId', 'subRecipeId', 'amount', 'unit'.")] string ingredientsJson,
        [Description("Recipe description")] string? description = null,
        [Description("Number of servings")] int? servings = null,
        [Description("Prep time in minutes")] int? prepTimeMinutes = null,
        [Description("Cook time in minutes")] int? cookTimeMinutes = null,
        [Description("JSON array of instruction step strings")] string? instructionsJson = null,
        [Description("Comma-separated tags")] string? tags = null,
        [Description("Make recipe public")] bool? isPublic = null,
        [Description("Image URL for the recipe")] string? imageUrl = null,
        [Description("Manual nutrition JSON. Object with: caloriesPerServing, proteinGrams, carbsGrams, fatGrams, fiberGrams")] string? nutritionJson = null,
        CancellationToken ct = default)
    {
        var ingredients = ParseIngredients(ingredientsJson);
        var instructions = ParseStringArray(instructionsJson);
        var tagList = ParseTags(tags);
        var nutrition = nutritionJson != null ? JsonSerializer.Deserialize<object>(nutritionJson) : null;

        return await _api.PutAsync($"/api/Recipes/{id}", new
        {
            id, title, description, servings, prepTimeMinutes, cookTimeMinutes,
            imageUrl, isPublic,
            ingredients, instructions, tags = tagList, nutrition
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

    /// <summary>
    /// Parse the LLM's ingredient JSON into strongly-typed objects.
    /// Handles various property naming conventions and auto-fills missing ingredientText.
    /// </summary>
    private static List<object> ParseIngredients(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var results = new List<object>();
        var index = 0;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            index++;

            // Handle plain strings: ["chicken breast", "rice"]
            if (el.ValueKind == JsonValueKind.String)
            {
                results.Add(new { ingredientText = el.GetString() ?? $"Ingredient {index}" });
                continue;
            }

            // Handle objects: [{"ingredientText": "chicken", "amount": 200}]
            var text = GetString(el, "ingredientText", "ingredient_text", "text", "name", "description", "ingredient");
            var foodId = GetString(el, "foodId", "food_id", "foodid");
            var subRecipeId = GetString(el, "subRecipeId", "sub_recipe_id", "subrecipeid");
            var amount = GetDecimal(el, "amount", "quantity", "grams", "weight");
            var unit = GetString(el, "unit", "units", "measurement");

            // Auto-fill ingredientText if missing
            if (string.IsNullOrWhiteSpace(text))
            {
                text = BuildIngredientText(amount, unit, index);
            }

            results.Add(new { ingredientText = text, foodId, subRecipeId, amount, unit });
        }

        return results;
    }

    private static string BuildIngredientText(decimal? amount, string? unit, int index)
    {
        if (amount.HasValue && !string.IsNullOrEmpty(unit))
            return $"{amount.Value}{unit}";
        if (amount.HasValue)
            return $"{amount.Value}g";
        return $"Ingredient {index}";
    }

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (el.TryGetProperty(key, out var prop))
            {
                return prop.ValueKind switch
                {
                    JsonValueKind.String => prop.GetString(),
                    JsonValueKind.Number => prop.ToString(),
                    _ => null
                };
            }
        }
        return null;
    }

    private static decimal? GetDecimal(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (el.TryGetProperty(key, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return prop.GetDecimal();
                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var d))
                    return d;
            }
        }
        return null;
    }

    private static List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    private static List<string> ParseTags(string? tags)
    {
        if (string.IsNullOrEmpty(tags)) return [];
        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
