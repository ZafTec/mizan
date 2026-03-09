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
    [Description("List the user's achievements and badges earned through consistent tracking.")]
    public async Task<string> ListAchievements(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Achievements?page={page}&pageSize={pageSize}", ct);
    }

    [McpServerTool(Name = "get_streak", ReadOnly = true, Idempotent = true)]
    [Description("Get the user's current tracking streak (consecutive days of logging meals).")]
    public async Task<string> GetStreak(CancellationToken ct = default)
    {
        return await _api.GetAsync("/api/Achievements/streak", ct);
    }

    [McpServerTool(Name = "update_streak")]
    [Description("Update/check-in the user's streak for today.")]
    public async Task<string> UpdateStreak(CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Achievements/streak", new { }, ct);
    }
}
