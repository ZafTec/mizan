using System.ComponentModel;
using System.Text.Json;
using Mizan.Contracts.Recipes;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class RecipeTools
{
    private readonly IBackendApiClient _api;

    public RecipeTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "search_recipes", ReadOnly = true, Idempotent = true)]
    [Description("Search for recipes. Returns paginated list with title, macros and prep/cook time.")]
    public async Task<string> SearchRecipes(
        [Description("Search term")] string? search = null,
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        [Description("Sort by: title, createdat")] string? sortBy = null,
        [Description("Sort direction: asc or desc")] string? sortOrder = null,
        [Description("Only return user's favorite recipes")] bool favoritesOnly = false,
        CancellationToken ct = default)
    {
        var qs = $"/api/Recipes?page={page}&pageSize={pageSize}&favoritesOnly={favoritesOnly}";
        if (!string.IsNullOrWhiteSpace(search)) qs += $"&searchTerm={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(sortBy)) qs += $"&sortBy={sortBy}";
        if (!string.IsNullOrEmpty(sortOrder)) qs += $"&sortOrder={sortOrder}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "get_recipe", ReadOnly = true, Idempotent = true)]
    [Description("Get full recipe details including ingredients, method and computed nutrition.")]
    public async Task<string> GetRecipe(
        [Description("Recipe UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Recipes/{id}", ct);
    }

    [McpServerTool(Name = "promote_to_recipe", Idempotent = false)]
    [Description("Save a logged meal as a private recipe. The date and mealType must contain at least two logged items. Ingredients and amounts come from the log. If the response asks for missing weights, ask the user and retry with recipeYieldsJson or entryWeightsGramsJson; never guess weights.")]
    public async Task<CallToolResult> PromoteToRecipe(
        [Description("Date of the logged meal, YYYY-MM-DD")] string date,
        [Description("Meal category: BREAKFAST, LUNCH, DINNER, SNACK, or DRINK")] string mealType,
        [Description("Name for the saved recipe")] string title,
        [Description("Optional household UUID to share the recipe with")] string? householdId = null,
        [Description("JSON object mapping recipe UUIDs to total finished recipe weights in grams, only when requested")] string? recipeYieldsJson = null,
        [Description("JSON object mapping manual diary entry UUIDs to the weight eaten in grams, only when requested")] string? entryWeightsGramsJson = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ArgumentException("Recipe title must contain 1 to 200 characters.", nameof(title));
        var request = new PromoteMealToRecipeRequest(
            ToolArguments.ParseDate(date, "date"), mealType.Trim().ToUpperInvariant(), title.Trim(),
            ToolArguments.ParseOptionalId(householdId, "householdId"),
            ParseWeights(recipeYieldsJson, "recipeYieldsJson"),
            ParseWeights(entryWeightsGramsJson, "entryWeightsGramsJson"));
        try
        {
            var result = await _api.PostAsync("/api/Recipes/promote", request, ct);
            return new CallToolResult { Content = [new TextContentBlock { Text = result }] };
        }
        catch (BackendApiException error) when (error.ErrorCode == "promotion_weights_required")
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = error.ResponseBody ?? error.Message }]
            };
        }
    }

    [McpServerTool(Name = "promote_recipe_to_preparation", Idempotent = true)]
    [Description("Derive a reusable ingredient from an owned recipe. Supply the finished weight of the entire batch in grams so per-100g nutrition stays correct; never guess it.")]
    public Task<string> PromoteRecipeToPreparation(
        [Description("Recipe UUID")] string id,
        [Description("Finished weight of the whole recipe in grams")] decimal yieldGrams,
        CancellationToken ct = default)
    {
        if (yieldGrams <= 0) throw new ArgumentException("yieldGrams must be positive.", nameof(yieldGrams));
        var recipeId = ToolArguments.ParseId(id, "id");
        return _api.PostAsync($"/api/Recipes/{recipeId}/preparation", new { YieldGrams = yieldGrams }, ct);
    }

    private static Dictionary<Guid, decimal>? ParseWeights(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var weights = JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(json)
                ?? throw new ArgumentException($"{name} must be an object of UUIDs and positive gram values.", name);
            if (weights.Any(item => item.Key == Guid.Empty || item.Value <= 0))
                throw new ArgumentException($"{name} must contain UUIDs and positive gram values.", name);
            return weights;
        }
        catch (JsonException error) { throw new ArgumentException($"Invalid {name}: {error.Message}", name); }
    }

    [McpServerTool(Name = "update_recipe")]
    [Description("Update an existing recipe. Only the recipe owner can update. Ingredients are fully replaced.")]
    public async Task<string> UpdateRecipe(
        [Description("Recipe UUID")] string id,
        [Description("Recipe title")] string title,
        [Description("JSON array of ingredients. Each object MUST have 'ingredientText' (human-readable). Optional: 'foodId', 'amount' (grams), 'unit'.")] string ingredientsJson,
        [Description("Recipe description")] string? description = null,
        [Description("Number of servings")] int? servings = null,
        [Description("Prep time in minutes")] int? prepTimeMinutes = null,
        [Description("Cook time in minutes")] int? cookTimeMinutes = null,
        [Description("Method as free text; newlines separate steps")] string? instructions = null,
        [Description("Make recipe public")] bool? isPublic = null,
        [Description("Image URL for the recipe")] string? imageUrl = null,
        CancellationToken ct = default)
    {
        var ingredients = ParseIngredients(ingredientsJson);

        return await _api.PutAsync($"/api/Recipes/{id}", new UpdateRecipeRequest
        {
            Id = ToolArguments.ParseId(id, "id"),
            Title = title,
            Description = description,
            Servings = servings ?? 1,
            PrepTimeMinutes = prepTimeMinutes,
            CookTimeMinutes = cookTimeMinutes,
            ImageUrl = imageUrl,
            IsPublic = isPublic ?? false,
            Ingredients = ingredients,
            Instructions = instructions,
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
    private static List<CreateRecipeIngredientDto> ParseIngredients(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var results = new List<CreateRecipeIngredientDto>();
        var index = 0;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            index++;

            // Handle plain strings: ["chicken breast", "rice"]
            if (el.ValueKind == JsonValueKind.String)
            {
                results.Add(new CreateRecipeIngredientDto
                {
                    IngredientText = el.GetString() ?? $"Ingredient {index}",
                });
                continue;
            }

            // Handle objects: [{"ingredientText": "chicken", "amount": 200}]
            var text = GetString(el, "ingredientText", "ingredient_text", "text", "name", "description", "ingredient");
            var foodId = GetString(el, "foodId", "food_id", "foodid");
            var amount = GetDecimal(el, "amount", "quantity", "grams", "weight");
            var unit = GetString(el, "unit", "units", "measurement");

            // Auto-fill ingredientText if missing
            if (string.IsNullOrWhiteSpace(text))
            {
                text = BuildIngredientText(amount, unit, index);
            }

            results.Add(new CreateRecipeIngredientDto
            {
                IngredientText = text,
                FoodId = ToolArguments.ParseOptionalId(foodId, "foodId"),
                Amount = amount,
                Unit = unit,
            });
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


}
