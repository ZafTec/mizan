using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class ProfileTools
{
    private readonly IBackendApiClient _api;

    public ProfileTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "get_my_profile", ReadOnly = true, Idempotent = true)]
    [Description("Get the current user's profile including name, email, role, and preferences.")]
    public async Task<string> GetMyProfile(CancellationToken ct = default)
    {
        return await _api.GetAsync("/api/Users/me", ct);
    }

    [McpServerTool(Name = "update_my_profile")]
    [Description("Update the current user's profile.")]
    public async Task<string> UpdateMyProfile(
        [Description("JSON body with profile fields to update (name, preferences, etc.)")] string body,
        CancellationToken ct = default)
    {
        return await _api.PutAsync("/api/Users/me", System.Text.Json.JsonSerializer.Deserialize<object>(body)!, ct);
    }

    [McpServerTool(Name = "export_profile", ReadOnly = true)]
    [Description("Export all user data as JSON (profile, meals, recipes, measurements, goals, etc.).")]
    public async Task<string> ExportProfile(CancellationToken ct = default)
    {
        return await _api.GetAsync("/api/Profile/export", ct);
    }
}
