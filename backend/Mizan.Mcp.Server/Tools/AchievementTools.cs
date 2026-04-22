using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class AchievementTools
{
    private readonly IBackendApiClient _api;

    public AchievementTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_achievements", ReadOnly = true, Idempotent = true)]
    [Description("List achievements earned or available. Supports search, category filter, sorting and pagination.")]
    public async Task<string> ListAchievements(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20, max 200)")] int pageSize = 20,
        [Description("Free-text search over name, description and category")] string? search = null,
        [Description("Filter by category (e.g. 'nutrition', 'workout')")] string? category = null,
        [Description("Sort by: name | category | points | threshold | criteriaType")] string? sortBy = null,
        [Description("Sort order: asc | desc")] string? sortOrder = null,
        CancellationToken ct = default)
    {
        var qs = new List<string>
        {
            $"Page={Math.Max(1, page)}",
            $"PageSize={Math.Clamp(pageSize, 1, 200)}"
        };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"SearchTerm={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(category)) qs.Add($"Category={Uri.EscapeDataString(category)}");
        if (!string.IsNullOrWhiteSpace(sortBy)) qs.Add($"SortBy={Uri.EscapeDataString(sortBy)}");
        if (!string.IsNullOrWhiteSpace(sortOrder)) qs.Add($"SortOrder={Uri.EscapeDataString(sortOrder)}");
        return await _api.GetAsync($"/api/Achievements?{string.Join('&', qs)}", ct);
    }

    [McpServerTool(Name = "get_streak", ReadOnly = true, Idempotent = true)]
    [Description("Get the user's current tracking streak. Defaults to nutrition; pass streakType='workout' for training streaks.")]
    public async Task<string> GetStreak(
        [Description("Streak type: nutrition | workout (default nutrition)")] string? streakType = null,
        CancellationToken ct = default)
    {
        var qs = string.IsNullOrWhiteSpace(streakType)
            ? string.Empty
            : $"?streakType={Uri.EscapeDataString(streakType)}";
        return await _api.GetAsync($"/api/Achievements/streak{qs}", ct);
    }
}
