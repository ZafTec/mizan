using System.ComponentModel;
using System.Text.Json;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class AdminTools
{
    private readonly IBackendApiClient _api; public AdminTools(IBackendApiClient api) => _api = api;
    [McpServerTool(Name = "admin_get_social_analytics", ReadOnly = true, Idempotent = true)] [Description("Admin only.")] public Task<string> SocialAnalytics(CancellationToken ct = default) => _api.GetAsync("/api/admin/social/analytics", ct);
    [McpServerTool(Name = "admin_list_content_reports", ReadOnly = true, Idempotent = true)] [Description("Admin only.")] public Task<string> Reports(string status = "Open", int page = 1, CancellationToken ct = default) => _api.GetAsync($"/api/admin/social/reports?status={status}&page={page}", ct);
    [McpServerTool(Name = "admin_resolve_content_report", Destructive = true)] [Description("Admin only. action is dismiss or delete.")] public Task<string> ResolveReport(string id, string action, string? note = null, CancellationToken ct = default) => _api.PostAsync($"/api/admin/social/reports/{id}/resolve", new { action, note }, ct);
    [McpServerTool(Name = "admin_list_audit_logs", ReadOnly = true, Idempotent = true)] [Description("Admin only.")] public Task<string> AuditLogs(int page = 1, int pageSize = 50, CancellationToken ct = default) => _api.GetAsync($"/api/AuditLogs?page={page}&pageSize={pageSize}", ct);
    [McpServerTool(Name = "admin_promote_exercise", Destructive = true)] [Description("Admin only. Promotes a user exercise into the global catalog.")] public Task<string> PromoteExercise(string id, CancellationToken ct = default) => _api.PostAsync($"/api/Exercises/{id}/promote", null, ct);
    [McpServerTool(Name = "admin_save_builtin_workout_template")] [Description("Admin only. Create or update a built-in template using the full JSON contract.")] public Task<string> SaveTemplate(string body, string? id = null, CancellationToken ct = default) => id is null ? _api.PostAsync("/api/WorkoutTemplates", JsonSerializer.Deserialize<object>(body), ct) : _api.PutAsync($"/api/WorkoutTemplates/{id}", JsonSerializer.Deserialize<object>(body)!, ct);
}
