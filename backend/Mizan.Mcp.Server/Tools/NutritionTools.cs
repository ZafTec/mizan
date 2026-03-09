using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class NutritionTools
{
    private readonly IBackendApiClient _api;

    public NutritionTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "get_daily_nutrition", ReadOnly = true, Idempotent = true)]
    [Description("Get daily nutrition summary including total calories, protein, carbs, fat, and fiber consumed. Also shows goal progress if a goal is set.")]
    public async Task<string> GetDailyNutrition(
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null,
        CancellationToken ct = default)
    {
        var qs = "/api/Nutrition/daily";
        if (!string.IsNullOrEmpty(date)) qs += $"?date={date}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "log_food")]
    [Description("Quick-log a food with custom macro values (without referencing an existing food/recipe).")]
    public async Task<string> LogFood(
        [Description("JSON body matching the LogFoodCommand schema")] string body,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Nutrition/log", System.Text.Json.JsonSerializer.Deserialize<object>(body), ct);
    }
}
