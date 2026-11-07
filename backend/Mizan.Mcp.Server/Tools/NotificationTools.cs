using System.ComponentModel;
using Mizan.Contracts.Notifications;
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

    [McpServerTool(Name = "get_notification_preferences", ReadOnly = true, Idempotent = true)]
    [Description("Which channels (inApp, telegram, push, email) each kind of notification uses, and the quiet hours (HH:mm in the person's time zone) during which push and Telegram wait. Telegram also needs telegramEnabled, the chat-wide switch, which needs a linked chat. Marketing is off until the person turns it on.")]
    public Task<string> Preferences(CancellationToken ct = default) => _api.GetAsync("/api/Notifications/preferences", ct);

    [McpServerTool(Name = "update_notification_preferences", Idempotent = true)]
    [Description("Change which channels some kinds of notification use, or the quiet hours, and get every preference back. Kinds: achievements, reminders, planning, coaching, household, social, account, marketing. A channel left out keeps its value. Set quiet hours with both quietHoursStart and quietHoursEnd as HH:mm (for example 22:00 and 07:00), or remove them with clearQuietHours. Only change what the person asked for.")]
    public Task<string> UpdatePreferences(
        [Description("Kinds to change, each with the channels to set: { kind, inApp?, telegram?, push?, email? }.")] NotificationKindPreferenceUpdate[]? kinds = null,
        [Description("Start of quiet hours, HH:mm.")] string? quietHoursStart = null,
        [Description("End of quiet hours, HH:mm.")] string? quietHoursEnd = null,
        [Description("True removes the quiet hours.")] bool? clearQuietHours = null,
        CancellationToken ct = default) =>
        _api.PutAsync("/api/Notifications/preferences", new UpdateNotificationPreferencesRequest(kinds, quietHoursStart, quietHoursEnd, clearQuietHours), ct);

    [McpServerTool(Name = "get_my_subscription", ReadOnly = true, Idempotent = true)]
    [Description("Get the current plan and entitlement status. Free MCP usage is limited to 15 successful tool calls per calendar month.")]
    public Task<string> Subscription(CancellationToken ct = default) => _api.GetAsync("/api/Subscriptions/me", ct);
}
