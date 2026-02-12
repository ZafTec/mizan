using System.Text.Json;
using Mizan.Mcp.Server.Models;

namespace Mizan.Mcp.Server.Services;

public class McpToolHandler
{
    private readonly IBackendClient _backend;
    private readonly ILogger<McpToolHandler> _logger;

    public McpToolHandler(IBackendClient backend, ILogger<McpToolHandler> logger)
    {
        _backend = backend;
        _logger = logger;
    }

    public List<McpTool> GetTools()
    {
        return new List<McpTool>
        {
            new McpTool
            {
                Name = "list_ingredients",
                Description = "Search for food ingredients in the database",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        search = new { type = "string", description = "Search term" },
                        limit = new { type = "integer", description = "Max results per page (default 20)" },
                        page = new { type = "integer", description = "Page number (default 1)" },
                        sortBy = new { type = "string", description = "Sort field: name, calories, protein, verified" },
                        sortOrder = new { type = "string", description = "Sort direction: asc or desc" }
                    }
                }
            },
            new McpTool
            {
                Name = "add_ingredient",
                Description = "Add a new food ingredient (Admin only)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string" },
                        brand = new { type = "string" },
                        caloriesPer100g = new { type = "integer" },
                        proteinPer100g = new { type = "number" },
                        carbsPer100g = new { type = "number" },
                        fatPer100g = new { type = "number" }
                    },
                    required = new[] { "name", "caloriesPer100g", "proteinPer100g", "carbsPer100g", "fatPer100g" }
                }
            },
            new McpTool
            {
                Name = "get_shopping_list",
                Description = "Get the user's shopping lists",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        page = new { type = "integer", description = "Page number (default 1)" },
                        limit = new { type = "integer", description = "Max results per page (default 20)" }
                    }
                }
            },
            new McpTool
            {
                Name = "get_nutrition_tracking",
                Description = "Get daily nutrition summary",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        date = new { type = "string", description = "Date in YYYY-MM-DD format (optional, defaults to today)" }
                    }
                }
            },
            new McpTool
            {
                Name = "list_recipes",
                Description = "Search for recipes",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        search = new { type = "string" },
                        page = new { type = "integer", description = "Page number (default 1)" },
                        limit = new { type = "integer", description = "Max results per page (default 20)" },
                        sortBy = new { type = "string", description = "Sort field: title, createdat" },
                        sortOrder = new { type = "string", description = "Sort direction: asc or desc" },
                        tags = new { type = "string", description = "Comma-separated tag list" },
                        favoritesOnly = new { type = "boolean", description = "Only return favorite recipes" }
                    }
                }
            },
            new McpTool
            {
                Name = "add_recipe",
                Description = "Create a new recipe",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string" },
                        description = new { type = "string" },
                        servings = new { type = "integer" },
                        prepTimeMinutes = new { type = "integer" },
                        cookTimeMinutes = new { type = "integer" }
                    },
                    required = new[] { "title" }
                }
            },
            new McpTool
            {
                Name = "log_meal",
                Description = "Log a food or recipe to the food diary",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        date = new { type = "string", description = "YYYY-MM-DD" },
                        mealType = new { type = "string", description = "Breakfast, Lunch, Dinner, Snack" },
                        foodId = new { type = "string", description = "UUID of food (optional)" },
                        recipeId = new { type = "string", description = "UUID of recipe (optional)" },
                        servings = new { type = "number" }
                    },
                    required = new[] { "date", "mealType", "servings" }
                }
            }
        };
    }

    public async Task<object> ExecuteToolAsync(Guid userId, string toolName, JsonElement args)
    {
        _logger.LogInformation("Executing tool {Tool} for user {User}", toolName, userId);

        var normalizedArgs = args;
        if (normalizedArgs.ValueKind == JsonValueKind.Null || normalizedArgs.ValueKind == JsonValueKind.Undefined)
        {
            using var emptyDoc = JsonDocument.Parse("{}");
            normalizedArgs = emptyDoc.RootElement.Clone();
        }

        switch (toolName)
        {
            case "list_ingredients":
                var search = normalizedArgs.TryGetProperty("search", out var s) ? s.GetString() : null;
                var limit = normalizedArgs.TryGetProperty("limit", out var l) ? l.GetInt32() : 20;
                var page = normalizedArgs.TryGetProperty("page", out var p) ? p.GetInt32() : 1;
                var sortBy = normalizedArgs.TryGetProperty("sortBy", out var sb) ? sb.GetString() : null;
                var sortOrder = normalizedArgs.TryGetProperty("sortOrder", out var so) ? so.GetString() : null;
                var qs = $"/api/Foods/search?pageSize={limit}&page={page}";
                if (!string.IsNullOrWhiteSpace(search)) qs += $"&searchTerm={Uri.EscapeDataString(search)}";
                if (!string.IsNullOrEmpty(sortBy)) qs += $"&sortBy={sortBy}";
                if (!string.IsNullOrEmpty(sortOrder)) qs += $"&sortOrder={sortOrder}";
                return await _backend.CallApiAsync(userId, "GET", qs);

            case "add_ingredient":
                return await _backend.CallApiAsync(userId, "POST", "/api/Foods", normalizedArgs);

            case "get_shopping_list":
                var slPage = normalizedArgs.TryGetProperty("page", out var slp) ? slp.GetInt32() : 1;
                var slLimit = normalizedArgs.TryGetProperty("limit", out var sll) ? sll.GetInt32() : 20;
                return await _backend.CallApiAsync(userId, "GET", $"/api/ShoppingLists?page={slPage}&pageSize={slLimit}");

            case "get_nutrition_tracking":
                var date = normalizedArgs.TryGetProperty("date", out var d) ? d.GetString() : DateTime.UtcNow.ToString("yyyy-MM-dd");
                return await _backend.CallApiAsync(userId, "GET", $"/api/Nutrition/daily?date={date}");

            case "list_recipes":
                var rSearch = normalizedArgs.TryGetProperty("search", out var rs) ? rs.GetString() : null;
                var rPage = normalizedArgs.TryGetProperty("page", out var rp) ? rp.GetInt32() : 1;
                var rLimit = normalizedArgs.TryGetProperty("limit", out var rl) ? rl.GetInt32() : 20;
                var rSortBy = normalizedArgs.TryGetProperty("sortBy", out var rsb) ? rsb.GetString() : null;
                var rSortOrder = normalizedArgs.TryGetProperty("sortOrder", out var rso) ? rso.GetString() : null;
                var rTags = normalizedArgs.TryGetProperty("tags", out var rt) ? rt.GetString() : null;
                var rFavOnly = normalizedArgs.TryGetProperty("favoritesOnly", out var rf) && rf.GetBoolean();
                var rqs = $"/api/Recipes?page={rPage}&pageSize={rLimit}&favoritesOnly={rFavOnly}";
                if (!string.IsNullOrWhiteSpace(rSearch)) rqs += $"&searchTerm={Uri.EscapeDataString(rSearch)}";
                if (!string.IsNullOrEmpty(rSortBy)) rqs += $"&sortBy={rSortBy}";
                if (!string.IsNullOrEmpty(rSortOrder)) rqs += $"&sortOrder={rSortOrder}";
                if (!string.IsNullOrEmpty(rTags))
                {
                    foreach (var tag in rTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        rqs += $"&tags={Uri.EscapeDataString(tag)}";
                    }
                }
                return await _backend.CallApiAsync(userId, "GET", rqs);

            case "add_recipe":
                if (!normalizedArgs.TryGetProperty("isPublic", out _))
                {
                    var recipeArgs = JsonSerializer.Deserialize<Dictionary<string, object?>>(normalizedArgs.GetRawText())
                        ?? new Dictionary<string, object?>();
                    recipeArgs["isPublic"] = true;
                    return await _backend.CallApiAsync(userId, "POST", "/api/Recipes", recipeArgs);
                }

                return await _backend.CallApiAsync(userId, "POST", "/api/Recipes", normalizedArgs);

            case "log_meal":
                return await _backend.CallApiAsync(userId, "POST", "/api/Meals", normalizedArgs);

            default:
                throw new Exception($"Tool {toolName} not found");
        }
    }
}
