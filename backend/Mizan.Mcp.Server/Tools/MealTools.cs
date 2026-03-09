using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class MealTools
{
    private readonly IBackendApiClient _api;

    public MealTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "get_food_diary", ReadOnly = true, Idempotent = true)]
    [Description("Get the food diary for a specific date. Shows all meals logged (breakfast, lunch, dinner, snack) with macros.")]
    public async Task<string> GetFoodDiary(
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null,
        CancellationToken ct = default)
    {
        var qs = "/api/Meals";
        if (!string.IsNullOrEmpty(date)) qs += $"?date={date}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "get_nutrition_range", ReadOnly = true, Idempotent = true)]
    [Description("Get daily nutrition summary over a date range. Useful for trends and weekly/monthly overview.")]
    public async Task<string> GetNutritionRange(
        [Description("Number of days to look back (1-90, default 7)")] int days = 7,
        [Description("End date in YYYY-MM-DD format (defaults to today)")] string? endDate = null,
        CancellationToken ct = default)
    {
        var qs = $"/api/Meals/range?days={Math.Clamp(days, 1, 90)}";
        if (!string.IsNullOrEmpty(endDate)) qs += $"&endDate={endDate}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "log_meal")]
    [Description("Log a food or recipe to the food diary. Provide either foodId or recipeId, not both.")]
    public async Task<string> LogMeal(
        [Description("Date in YYYY-MM-DD format")] string date,
        [Description("Meal type: Breakfast, Lunch, Dinner, or Snack")] string mealType,
        [Description("Number of servings")] decimal servings,
        [Description("Food UUID (provide this OR recipeId)")] string? foodId = null,
        [Description("Recipe UUID (provide this OR foodId)")] string? recipeId = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Meals", new
        {
            date, mealType, servings, foodId, recipeId
        }, ct);
    }

    [McpServerTool(Name = "delete_meal", Destructive = true)]
    [Description("Delete a food diary entry. This removes the logged meal.")]
    public async Task<string> DeleteMeal(
        [Description("Diary entry UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/Meals/{id}", ct);
    }
}
