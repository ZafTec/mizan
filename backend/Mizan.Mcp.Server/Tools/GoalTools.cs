using System.ComponentModel;
using Mizan.Contracts.Users;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class GoalTools
{
    private readonly IBackendApiClient _api;

    public GoalTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "get_current_goal", ReadOnly = true, Idempotent = true)]
    [Description("Get the user's current nutrition/fitness goal including target calories and macros. With no active goal, returns {\"goal\":null} and says so.")]
    public async Task<string> GetCurrentGoal(CancellationToken ct = default)
    {
        var goal = await _api.GetAsync("/api/Goals", ct);
        return string.IsNullOrWhiteSpace(goal) || goal.Trim() == "null"
            ? System.Text.Json.JsonSerializer.Serialize(new { goal = (object?)null, message = "No active goal. Set one with create_goal." })
            : goal;
    }

    [McpServerTool(Name = "get_goal_history", ReadOnly = true, Idempotent = true)]
    [Description("Get history of all goals the user has set over time.")]
    public async Task<string> GetGoalHistory(CancellationToken ct = default)
    {
        return await _api.GetAsync("/api/Goals/history", ct);
    }

    [McpServerTool(Name = "create_goal")]
    [Description("Set a new nutrition/fitness goal. This replaces the active goal; the old one stays in the history. Every target is optional.")]
    public async Task<string> CreateGoal(
        [Description("Goal type: " + Mizan.Domain.Goals.GoalTypes.Described)] string? goalType = null,
        [Description("Target daily calories")] int? targetCalories = null,
        [Description("Target protein grams a day")] decimal? targetProteinGrams = null,
        [Description("Target carbs grams a day")] decimal? targetCarbsGrams = null,
        [Description("Target fat grams a day")] decimal? targetFatGrams = null,
        [Description("Target fiber grams a day")] decimal? targetFiberGrams = null,
        [Description("Target weight in kg")] decimal? targetWeightKg = null,
        [Description("Target date, YYYY-MM-DD")] string? targetDate = null,
        CancellationToken ct = default)
    {
        var date = ToolArguments.ParseOptionalDate(targetDate, "targetDate");
        return await _api.PostAsync("/api/Goals", new CreateGoalRequest(
            goalType, targetCalories, targetProteinGrams, targetCarbsGrams, targetFatGrams, targetFiberGrams,
            targetWeightKg, targetWeightKg.HasValue ? "kg" : null, date), ct);
    }

    [McpServerTool(Name = "record_goal_progress")]
    [Description("Record a day's check-in against the active goal. A goal keeps one check-in a day: recording the same day again replaces it. "
        + "Calories and macros come from that day's diary unless you pass them; "
        + "a weight left out comes from that day's body measurement. A weight needs body write permission as well. "
        + "Returns what was stored.")]
    public async Task<string> RecordProgress(
        [Description("Weight in kg (optional)")] decimal? currentWeightKg = null,
        [Description("Notes about progress (optional)")] string? notes = null,
        [Description("Day, YYYY-MM-DD (defaults to today in the person's time zone)")] string? date = null,
        CancellationToken ct = default)
    {
        var day = ToolArguments.ParseOptionalDate(date, "date");
        return await _api.PostAsync("/api/Goals/progress", new RecordGoalProgressRequest(day, currentWeightKg, notes), ct);
    }

    [McpServerTool(Name = "delete_goal", Destructive = true)]
    [Description("Delete one of the person's goals and its check-ins. Deleting the active goal leaves no goal active. This is permanent.")]
    public async Task<string> DeleteGoal([Description("Goal UUID, from get_current_goal or get_goal_history")] string id, CancellationToken ct = default)
    {
        var goalId = ToolArguments.ParseId(id, "id");
        return await _api.DeleteAsync($"/api/Goals/{goalId}", ct);
    }

    [McpServerTool(Name = "delete_goal_progress", Destructive = true)]
    [Description("Delete one goal check-in. This is permanent.")]
    public async Task<string> DeleteProgress([Description("Check-in UUID, from get_goal_progress")] string id, CancellationToken ct = default)
    {
        var entryId = ToolArguments.ParseId(id, "id");
        return await _api.DeleteAsync($"/api/Goals/progress/{entryId}", ct);
    }

    [McpServerTool(Name = "get_goal_progress", ReadOnly = true, Idempotent = true)]
    [Description("Get goal progress history over a time period.")]
    public async Task<string> GetProgressHistory(
        [Description("Number of days to look back (default 30)")] int days = 30,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Goals/progress?days={days}", ct);
    }
}
