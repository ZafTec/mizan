using System.ComponentModel;
using Mizan.Contracts.Notifications;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

/// <summary>
/// Notification campaigns and lifecycle messages, as tools over the admin endpoints the console uses
/// (docs/ADMIN.md#notification-campaigns). The API resolves audiences, checks the confirmed count and
/// audits every change; nothing here knows a rule.
/// </summary>
[McpServerToolType]
public sealed class AdminNotificationTools
{
    private const string Base = "/api/admin/notification-campaigns";
    private readonly IBackendApiClient _api;

    public AdminNotificationTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "admin_list_notification_campaigns", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Every campaign and lifecycle message, newest first, with its phase and delivery totals.")]
    public Task<string> List(CancellationToken ct = default) => _api.GetAsync(Base, ct);

    [McpServerTool(Name = "admin_get_notification_campaign", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. One campaign with its message, audience, timing and the delivery report per channel: queued, sent, failed, skipped and, in the app, opened.")]
    public Task<string> Get(string campaignId, CancellationToken ct = default) =>
        _api.GetAsync($"{Base}/{ToolArguments.ParseId(campaignId, "campaignId")}", ct);

    [McpServerTool(Name = "admin_preview_audience", ReadOnly = true, Idempotent = true)]
    [Description(
        "Admin only. How many people an audience reaches now, before anything is sent: matched by the filters, "
        + "reachable on at least one channel, and per channel. purpose is marketing (only people who opted in) or lifecycle. "
        + "Filters: plans (free, pro, lifetime), signedUpFrom and signedUpTo (yyyy-MM-dd), inactiveDays, telegramLinked, "
        + "hasHousehold, timeZones (IANA). channels: inApp, telegram, push, email.")]
    public Task<string> PreviewAudience(string purpose, CampaignAudienceFilter? audience = null, IReadOnlyList<string>? channels = null, CancellationToken ct = default) =>
        _api.PostAsync($"{Base}/audience", new CampaignAudienceRequest(purpose, audience, channels), ct);

    [McpServerTool(Name = "admin_create_notification_campaign")]
    [Description(
        "Admin only. Saves a campaign as a draft; nothing is sent until it is started with admin_activate_notification_campaign. "
        + "purpose is marketing or lifecycle. message has title, body, imageUrl, linkPath (an app page such as /today) and up to two "
        + "actions (label and path); the title, body and labels may use {firstName} and {appLink}. "
        + "timing.send is now, once (date, time), daily (time), days (days, time), every (everyDays or everyHours, date, time) "
        + "or event: user.signed_up (eventDays after sign-up), user.inactive (eventDays without activity) or "
        + "subscription.pro_ends (eventDays before Pro ends). Times are on timeZoneId's clock, UTC when left out.")]
    public Task<string> Create(
        string name,
        string purpose,
        CampaignContent message,
        CampaignTiming timing,
        CampaignAudienceFilter? audience = null,
        IReadOnlyList<string>? channels = null,
        string? timeZoneId = null,
        CancellationToken ct = default) =>
        _api.PostAsync(Base, new SaveNotificationCampaignRequest(name, purpose, message, timing, audience, channels, timeZoneId), ct);

    [McpServerTool(Name = "admin_send_test_notification")]
    [Description("Admin only. Sends the campaign to you alone, now, on each of its channels, with your name in the placeholders. It names any channel you cannot receive on and why.")]
    public Task<string> SendTest(string campaignId, CancellationToken ct = default) =>
        _api.PostAsync($"{Base}/{ToolArguments.ParseId(campaignId, "campaignId")}/test", null, ct);

    [McpServerTool(Name = "admin_activate_notification_campaign", Destructive = true)]
    [Description(
        "Admin only. Starts a draft campaign. audienceCount is the reachable count from admin_preview_audience, which you "
        + "confirm with the person first; when the audience is a different size now the API refuses with the new count. "
        + "A campaign sent now goes out at once.")]
    public Task<string> Activate(string campaignId, int audienceCount, CancellationToken ct = default) =>
        _api.PostAsync($"{Base}/{ToolArguments.ParseId(campaignId, "campaignId")}/activate", new ActivateNotificationCampaignRequest(audienceCount), ct);

    [McpServerTool(Name = "admin_pause_campaign", Destructive = true, Idempotent = true)]
    [Description("Admin only. Pauses an active campaign. Deliveries due while it is paused are not sent.")]
    public Task<string> Pause(string campaignId, CancellationToken ct = default) =>
        _api.PostAsync($"{Base}/{ToolArguments.ParseId(campaignId, "campaignId")}/pause", null, ct);

    [McpServerTool(Name = "admin_resume_campaign", Destructive = true, Idempotent = true)]
    [Description("Admin only. Starts a paused campaign again from its next occurrence.")]
    public Task<string> Resume(string campaignId, CancellationToken ct = default) =>
        _api.PostAsync($"{Base}/{ToolArguments.ParseId(campaignId, "campaignId")}/resume", null, ct);

    [McpServerTool(Name = "admin_cancel_campaign", Destructive = true, Idempotent = true)]
    [Description("Admin only. Ends a campaign for good. Nothing queued goes out and it cannot be started again.")]
    public Task<string> Cancel(string campaignId, CancellationToken ct = default) =>
        _api.PostAsync($"{Base}/{ToolArguments.ParseId(campaignId, "campaignId")}/cancel", null, ct);
}
