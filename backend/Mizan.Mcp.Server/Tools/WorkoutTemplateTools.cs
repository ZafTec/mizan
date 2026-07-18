using System.ComponentModel;
using System.Text.Json;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class WorkoutTemplateTools
{
    private readonly IBackendApiClient _api; public WorkoutTemplateTools(IBackendApiClient api) => _api = api;
    [McpServerTool(Name = "list_workout_templates", ReadOnly = true, Idempotent = true)] public Task<string> List(CancellationToken ct = default) => _api.GetAsync("/api/WorkoutTemplates", ct);
    [McpServerTool(Name = "get_next_workout_session", ReadOnly = true, Idempotent = true)] public Task<string> Next(string templateId, CancellationToken ct = default) => _api.GetAsync($"/api/WorkoutTemplates/{templateId}/next-session", ct);
    [McpServerTool(Name = "create_workout_template")]
    [Description("Create a template from the full JSON template contract.")]
    public Task<string> Create(string body, CancellationToken ct = default) => _api.PostAsync("/api/WorkoutTemplates", JsonSerializer.Deserialize<object>(body), ct);
    [McpServerTool(Name = "update_workout_template", Idempotent = true)] public Task<string> Update(string id, string body, CancellationToken ct = default) => _api.PutAsync($"/api/WorkoutTemplates/{id}", JsonSerializer.Deserialize<object>(body)!, ct);
    [McpServerTool(Name = "duplicate_workout_template")] public Task<string> Duplicate(string id, CancellationToken ct = default) => _api.PostAsync($"/api/WorkoutTemplates/{id}/duplicate", null, ct);
    [McpServerTool(Name = "delete_workout_template", Destructive = true)] public Task<string> Delete(string id, CancellationToken ct = default) => _api.DeleteAsync($"/api/WorkoutTemplates/{id}", ct);
}
