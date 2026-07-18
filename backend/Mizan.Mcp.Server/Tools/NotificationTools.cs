using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class NotificationTools
{
    private readonly IBackendApiClient _api; public NotificationTools(IBackendApiClient api) => _api = api;
    [McpServerTool(Name = "list_notifications", ReadOnly = true, Idempotent = true)] public Task<string> List(int page = 1, int pageSize = 20, CancellationToken ct = default) => _api.GetAsync($"/api/Notifications?page={page}&pageSize={pageSize}", ct);
    [McpServerTool(Name = "get_unread_notification_count", ReadOnly = true, Idempotent = true)] public Task<string> Count(CancellationToken ct = default) => _api.GetAsync("/api/Notifications/unread-count", ct);
    [McpServerTool(Name = "mark_notification_read", Idempotent = true)] public Task<string> Read(string id, CancellationToken ct = default) => _api.PostAsync($"/api/Notifications/{id}/read", null, ct);
    [McpServerTool(Name = "mark_all_notifications_read", Idempotent = true)] public Task<string> ReadAll(CancellationToken ct = default) => _api.PostAsync("/api/Notifications/read-all", null, ct);
    [McpServerTool(Name = "get_my_subscription", ReadOnly = true, Idempotent = true)]
    [Description("Get the current plan and entitlement status. Free MCP usage is limited to 15 successful tool calls per calendar month.")]
    public Task<string> Subscription(CancellationToken ct = default) => _api.GetAsync("/api/Subscriptions/me", ct);
}
