using System.ComponentModel;
using System.Text.Json;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class MealTools
{
    private readonly IBackendApiClient _api;

    public MealTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "get_food_diary", ReadOnly = true, Idempotent = true)]
    [Description("Get the food diary for a specific date. Shows all meals logged with macros.")]
    public async Task<string> GetFoodDiary(
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null,
        CancellationToken ct = default)
    {
        var qs = "/api/Meals";
        if (!string.IsNullOrEmpty(date)) qs += $"?date={date}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "get_daily_nutrition", ReadOnly = true, Idempotent = true)]
    [Description("Get daily nutrition totals for a specific date. Shows calories, protein, carbs, fat breakdown.")]
    public async Task<string> GetDailyNutrition(
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null,
        CancellationToken ct = default)
    {
        var qs = "/api/Meals/range?days=1";
        if (!string.IsNullOrEmpty(date)) qs += $"&endDate={date}";
        var raw = await _api.GetAsync(qs, ct);

        using var doc = JsonDocument.Parse(raw);
        var days = doc.RootElement.GetProperty("days");

        if (days.GetArrayLength() == 0)
        {
            var targetDate = date ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
            return JsonSerializer.Serialize(new { date = targetDate, totalCalories = 0m, totalProtein = 0m, totalCarbs = 0m, totalFat = 0m });
        }

        var day = days[0];
        return JsonSerializer.Serialize(new
        {
            date = day.GetProperty("date").ToString(),
            totalCalories = day.GetProperty("calories").GetDecimal(),
            totalProtein = day.GetProperty("protein").GetDecimal(),
            totalCarbs = day.GetProperty("carbs").GetDecimal(),
            totalFat = day.GetProperty("fat").GetDecimal()
        });
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

    [McpServerTool(Name = "log_food")]
    [Description("Log a food item to the food diary. Use search_foods first to get a foodId.")]
    public async Task<string> LogFood(
        [Description("Food UUID from search_foods")] string foodId,
        [Description("Date in YYYY-MM-DD format")] string date,
        [Description("Meal category: MEAL, SNACK, or DRINK")] string mealType = "MEAL",
        [Description("Number of servings (default 1)")] decimal servings = 1,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Meals", new
        {
            entryDate = date,
            mealType = NormalizeMealType(mealType),
            servings,
            foodId
        }, ct);
    }

    [McpServerTool(Name = "log_meal")]
    [Description("Log a recipe to the food diary. Use search_recipes or get_recipe first to get a recipeId.")]
    public async Task<string> LogMeal(
        [Description("Recipe UUID from search_recipes")] string recipeId,
        [Description("Date in YYYY-MM-DD format")] string date,
        [Description("Meal category: MEAL, SNACK, or DRINK")] string mealType = "MEAL",
        [Description("Number of servings (default 1)")] decimal servings = 1,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Meals", new
        {
            entryDate = date,
            mealType = NormalizeMealType(mealType),
            servings,
            recipeId
        }, ct);
    }

    [McpServerTool(Name = "log_meal_manual")]
    [Description("Log a meal with manual nutrition values when no food/recipe exists in the database.")]
    public async Task<string> LogMealManual(
        [Description("Meal name (e.g. 'Homemade smoothie')")] string name,
        [Description("Date in YYYY-MM-DD format")] string date,
        [Description("Total calories")] decimal calories,
        [Description("Meal category: MEAL, SNACK, or DRINK")] string mealType = "MEAL",
        [Description("Number of servings (default 1)")] decimal servings = 1,
        [Description("Protein in grams")] decimal? proteinGrams = null,
        [Description("Carbs in grams")] decimal? carbsGrams = null,
        [Description("Fat in grams")] decimal? fatGrams = null,
        [Description("Fiber in grams")] decimal? fiberGrams = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Meals", new
        {
            entryDate = date,
            mealType = NormalizeMealType(mealType),
            servings,
            name,
            calories,
            proteinGrams,
            carbsGrams,
            fatGrams,
            fiberGrams
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

    private static string NormalizeMealType(string mealType)
    {
        return mealType.Trim().ToUpperInvariant() switch
        {
            "MEAL" or "BREAKFAST" or "LUNCH" or "DINNER" => "MEAL",
            "SNACK" => "SNACK",
            "DRINK" or "BEVERAGE" => "DRINK",
            var raw => throw new ArgumentException($"Meal type '{raw}' is invalid. Use MEAL, SNACK, or DRINK.")
        };
    }
}
