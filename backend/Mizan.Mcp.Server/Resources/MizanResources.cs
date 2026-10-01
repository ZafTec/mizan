using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using Mizan.Mcp.Server.Tools;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Resources;

/// <summary>
/// Things a client can attach to a conversation without a tool call: a day's
/// diary, a recipe, today's totals. Each one reads exactly what the matching
/// tool reads, through the same API, so the same permissions and household
/// limits apply (docs/MCP.md#resources).
/// </summary>
[McpServerResourceType]
public sealed class MizanResources
{
    private readonly IBackendApiClient _api;

    public MizanResources(IBackendApiClient api) => _api = api;

    [McpServerResource(UriTemplate = "mizan://profile", Name = "profile", Title = "My profile", MimeType = "application/json")]
    [Description("The signed-in person's profile and preferences.")]
    public Task<string> Profile(CancellationToken ct) => new ProfileTools(_api).GetMyProfile(ct);

    [McpServerResource(UriTemplate = "mizan://goals/current", Name = "current-goal", Title = "Current goal", MimeType = "application/json")]
    [Description("The active nutrition and fitness goal with calorie and macro targets.")]
    public Task<string> CurrentGoal(CancellationToken ct) => new GoalTools(_api).GetCurrentGoal(ct);

    [McpServerResource(UriTemplate = "mizan://nutrition/today", Name = "nutrition-today", Title = "Today's nutrition", MimeType = "application/json")]
    [Description("Today's calorie and macro totals against the goal.")]
    public Task<string> NutritionToday(CancellationToken ct) => new NutritionTools(_api).GetNutritionSummary(null, ct);

    [McpServerResource(UriTemplate = "mizan://streak", Name = "streak", Title = "Logging streak", MimeType = "application/json")]
    [Description("The current and longest logging streak.")]
    public Task<string> Streak(CancellationToken ct) => new AchievementTools(_api).GetStreak(null, ct);

    [McpServerResource(UriTemplate = "mizan://households", Name = "households", Title = "My households", MimeType = "application/json")]
    [Description("The households the connection may see, and which one is active.")]
    public Task<string> Households(CancellationToken ct) => new HouseholdTools(_api).ListMine(ct);

    [McpServerResource(UriTemplate = "mizan://diary/{date}", Name = "food-diary", Title = "Food diary for a day", MimeType = "application/json")]
    [Description("Every meal logged on one date (YYYY-MM-DD) with its macros.")]
    public Task<string> Diary(string date, CancellationToken ct) => new MealTools(_api).GetFoodDiary(date, ct);

    [McpServerResource(UriTemplate = "mizan://nutrition/{date}", Name = "nutrition-day", Title = "Nutrition totals for a day", MimeType = "application/json")]
    [Description("Calorie and macro totals for one date (YYYY-MM-DD).")]
    public Task<string> NutritionDay(string date, CancellationToken ct) => new MealTools(_api).GetDailyNutrition(date, ct);

    [McpServerResource(UriTemplate = "mizan://recipes/{id}", Name = "recipe", Title = "Recipe", MimeType = "application/json")]
    [Description("A recipe with ingredients, method and computed nutrition.")]
    public Task<string> Recipe(string id, CancellationToken ct) => new RecipeTools(_api).GetRecipe(id, ct);

    [McpServerResource(UriTemplate = "mizan://workouts/{id}", Name = "workout", Title = "Workout", MimeType = "application/json")]
    [Description("One logged workout with its sets.")]
    public Task<string> Workout(string id, CancellationToken ct) => new WorkoutTools(_api).GetWorkout(id, ct);

    [McpServerResource(UriTemplate = "mizan://meal-plans/{id}", Name = "meal-plan", Title = "Meal plan", MimeType = "application/json")]
    [Description("A meal plan with its scheduled recipes and nutrition totals.")]
    public Task<string> MealPlan(string id, CancellationToken ct) => new MealPlanTools(_api).GetMealPlan(id, ct);

    [McpServerResource(UriTemplate = "mizan://shopping-lists/{id}", Name = "shopping-list", Title = "Shopping list", MimeType = "application/json")]
    [Description("A shopping list with its items and which are bought.")]
    public Task<string> ShoppingList(string id, CancellationToken ct) => new ShoppingListTools(_api).GetShoppingList(id, ct);
}
