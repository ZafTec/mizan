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

    [McpServerTool(Name = "admin_get_achievement", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Get an achievement definition by UUID.")]
    public Task<string> AdminGet(string id, CancellationToken ct = default) => _api.GetAsync($"/api/Achievements/{id}", ct);

    [McpServerTool(Name = "admin_create_achievement")]
    [Description("Admin only. Create an achievement criterion.")]
    public Task<string> AdminCreate(string name, string description, int points, string category, string criteriaType, int threshold, string? iconUrl = null, CancellationToken ct = default)
        => _api.PostAsync("/api/Achievements", new { name, description, points, category, criteriaType, threshold, iconUrl }, ct);

    [McpServerTool(Name = "admin_update_achievement", Idempotent = true)]
    [Description("Admin only. Update an achievement criterion.")]
    public Task<string> AdminUpdate(string id, string name, string description, int points, string category, string criteriaType, int threshold, string? iconUrl = null, CancellationToken ct = default)
        => _api.PutAsync($"/api/Achievements/{id}", new { id, name, description, points, category, criteriaType, threshold, iconUrl }, ct);

    [McpServerTool(Name = "admin_delete_achievement", Destructive = true)]
    [Description("Admin only. Delete an achievement definition.")]
    public Task<string> AdminDelete(string id, CancellationToken ct = default) => _api.DeleteAsync($"/api/Achievements/{id}", ct);

    [McpServerTool(Name = "admin_get_achievement_analytics", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Read achievement unlock analytics.")]
    public Task<string> AdminAnalytics(int page = 1, int pageSize = 50, CancellationToken ct = default)
        => _api.GetAsync($"/api/Achievements/analytics?page={page}&pageSize={pageSize}", ct);
}
