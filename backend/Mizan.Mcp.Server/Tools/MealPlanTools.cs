using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class MealPlanTools
{
    private readonly IBackendApiClient _api;

    public MealPlanTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_meal_plans", ReadOnly = true, Idempotent = true)]
    [Description("List the user's meal plans. Meal plans organize recipes across dates and meal types.")]
    public async Task<string> ListMealPlans(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/MealPlans?page={page}&pageSize={pageSize}", ct);
    }

    [McpServerTool(Name = "get_meal_plan", ReadOnly = true, Idempotent = true)]
    [Description("Get a meal plan with all its scheduled recipes, dates, and nutritional totals.")]
    public async Task<string> GetMealPlan(
        [Description("Meal plan UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/MealPlans/{id}", ct);
    }

    [McpServerTool(Name = "create_meal_plan")]
    [Description("Create a new meal plan with a name and date range.")]
    public async Task<string> CreateMealPlan(
        [Description("Plan name")] string name,
        [Description("Start date YYYY-MM-DD")] string startDate,
        [Description("End date YYYY-MM-DD")] string endDate,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/MealPlans", new { name, startDate, endDate }, ct);
    }

    [McpServerTool(Name = "update_meal_plan")]
    [Description("Update a meal plan's name or date range.")]
    public async Task<string> UpdateMealPlan(
        [Description("Meal plan UUID")] string id,
        [Description("Plan name")] string name,
        [Description("Start date YYYY-MM-DD")] string startDate,
        [Description("End date YYYY-MM-DD")] string endDate,
        CancellationToken ct = default)
    {
        return await _api.PutAsync($"/api/MealPlans/{id}", new { name, startDate, endDate }, ct);
    }

    [McpServerTool(Name = "delete_meal_plan", Destructive = true)]
    [Description("Delete a meal plan and all its scheduled recipes. This is permanent.")]
    public async Task<string> DeleteMealPlan(
        [Description("Meal plan UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/MealPlans/{id}", ct);
    }

    [McpServerTool(Name = "add_recipe_to_meal_plan")]
    [Description("Add a recipe to a meal plan on a specific date and meal type.")]
    public async Task<string> AddRecipe(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Recipe UUID")] string recipeId,
        [Description("Date YYYY-MM-DD")] string date,
        [Description("Meal type: breakfast, lunch, dinner, snack")] string mealType = "dinner",
        [Description("Number of servings")] decimal servings = 1,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/MealPlans/{mealPlanId}/recipes", new
        {
            recipeId, date, mealType, servings
        }, ct);
    }

    [McpServerTool(Name = "remove_recipe_from_meal_plan", Destructive = true)]
    [Description("Remove a recipe from a meal plan.")]
    public async Task<string> RemoveRecipe(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Recipe UUID")] string recipeId,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/MealPlans/{mealPlanId}/recipes/{recipeId}", ct);
    }

    [McpServerTool(Name = "update_meal_plan_recipe")]
    [Description("Update a recipe's date, meal type, or servings within a meal plan.")]
    public async Task<string> UpdateRecipe(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Recipe UUID")] string recipeId,
        [Description("Date YYYY-MM-DD")] string date,
        [Description("Meal type: breakfast, lunch, dinner, snack")] string mealType = "dinner",
        [Description("Number of servings")] decimal servings = 1,
        CancellationToken ct = default)
    {
        return await _api.PutAsync($"/api/MealPlans/{mealPlanId}/recipes/{recipeId}", new
        {
            date, mealType, servings
        }, ct);
    }
}
