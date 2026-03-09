using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class GoalTools
{
    private readonly IBackendApiClient _api;

    public GoalTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "get_current_goal", ReadOnly = true, Idempotent = true)]
    [Description("Get the user's current nutrition/fitness goal including target calories and macros.")]
    public async Task<string> GetCurrentGoal(CancellationToken ct = default)
    {
        return await _api.GetAsync("/api/Goals", ct);
    }

    [McpServerTool(Name = "get_goal_history", ReadOnly = true, Idempotent = true)]
    [Description("Get history of all goals the user has set over time.")]
    public async Task<string> GetGoalHistory(CancellationToken ct = default)
    {
        return await _api.GetAsync("/api/Goals/history", ct);
    }

    [McpServerTool(Name = "create_goal")]
    [Description("Set a new nutrition/fitness goal. This replaces any existing active goal.")]
    public async Task<string> CreateGoal(
        [Description("Goal type: weight_loss, muscle_gain, maintenance, custom")] string goalType,
        [Description("Target daily calories")] int targetCalories,
        [Description("Target protein grams")] decimal targetProteinGrams,
        [Description("Target carbs grams")] decimal targetCarbsGrams,
        [Description("Target fat grams")] decimal targetFatGrams,
        [Description("Target fiber grams (optional)")] decimal? targetFiberGrams = null,
        [Description("Target weight in kg (optional)")] decimal? targetWeightKg = null,
        [Description("Target date in YYYY-MM-DD format (optional)")] string? targetDate = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Goals", new
        {
            goalType, targetCalories, targetProteinGrams, targetCarbsGrams, targetFatGrams,
            targetFiberGrams, targetWeightKg, targetDate
        }, ct);
    }

    [McpServerTool(Name = "record_goal_progress")]
    [Description("Record a progress entry for the current goal (e.g., weight check-in).")]
    public async Task<string> RecordProgress(
        [Description("Current weight in kg (optional)")] decimal? currentWeightKg = null,
        [Description("Notes about progress (optional)")] string? notes = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Goals/progress", new { currentWeightKg, notes }, ct);
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
