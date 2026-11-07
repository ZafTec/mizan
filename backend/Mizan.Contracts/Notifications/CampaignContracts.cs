namespace Mizan.Contracts.Notifications;

/// <summary>
/// Who a campaign reaches (docs/ADMIN.md#notification-campaigns). Every filter left out matches
/// everyone; the filters given must all match.
/// </summary>
/// <param name="Plans">free, pro and lifetime; empty is every plan.</param>
/// <param name="SignedUpFrom">yyyy-MM-dd, the first sign-up day included (UTC).</param>
/// <param name="SignedUpTo">yyyy-MM-dd, the last sign-up day included (UTC).</param>
/// <param name="InactiveDays">No visit and nothing logged for at least this many days.</param>
/// <param name="TelegramLinked">true: a linked Telegram chat; false: none.</param>
/// <param name="HasHousehold">true: a member of a household; false: in none.</param>
/// <param name="TimeZones">IANA zones, such as Africa/Addis_Ababa. A person without one counts as UTC.</param>
public record CampaignAudienceFilter(
    IReadOnlyList<string>? Plans = null,
    string? SignedUpFrom = null,
    string? SignedUpTo = null,
    int? InactiveDays = null,
    bool? TelegramLinked = null,
    bool? HasHousehold = null,
    IReadOnlyList<string>? TimeZones = null);

/// <summary>
/// When a campaign sends. <c>now</c> sends when it is started; <c>once</c> at <see cref="Date"/> and
/// <see cref="Time"/>; <c>daily</c>, <c>days</c> (with <see cref="Days"/>) and <c>every</c> (with
/// <see cref="EveryDays"/> or <see cref="EveryHours"/>) repeat at <see cref="Time"/> on the campaign's
/// clock; <c>event</c> follows <see cref="Event"/> by <see cref="EventDays"/>.
/// </summary>
/// <param name="Send">now, once, daily, days, every or event.</param>
/// <param name="Date">yyyy-MM-dd: the day of once, or the first day of every.</param>
/// <param name="Time">HH:mm on the campaign's clock.</param>
/// <param name="Days">mon to sun, for days.</param>
/// <param name="Event">user.signed_up (days after), user.inactive (quiet days) or subscription.pro_ends (days before).</param>
public record CampaignTiming(
    string Send,
    string? Date = null,
    string? Time = null,
    IReadOnlyList<string>? Days = null,
    int? EveryDays = null,
    int? EveryHours = null,
    string? Event = null,
    int? EventDays = null);

/// <summary>
/// What a campaign says: the structured message every channel renders. The title, body and actions may
/// hold <c>{firstName}</c> and <c>{appLink}</c>, which each recipient gets filled in.
/// </summary>
/// <param name="LinkPath">The app page the notice opens, such as <c>/today</c>.</param>
/// <param name="Actions">Up to two buttons, each a label and an app path.</param>
public record CampaignContent(
    string Title,
    string? Body = null,
    string? ImageUrl = null,
    string? LinkPath = null,
    IReadOnlyList<NotificationAction>? Actions = null);

/// <summary>Body of <c>POST /api/admin/notification-campaigns</c> and <c>PUT /{id}</c>.</summary>
/// <param name="Purpose">marketing (news and offers, only to people who opted in) or lifecycle (a product message).</param>
/// <param name="Channels">inApp, telegram, push, email; all four when left out.</param>
/// <param name="TimeZoneId">The clock a recurring or once campaign's time is read on; UTC when left out.</param>
public record SaveNotificationCampaignRequest(
    string Name,
    string Purpose,
    CampaignContent Message,
    CampaignTiming Timing,
    CampaignAudienceFilter? Audience = null,
    IReadOnlyList<string>? Channels = null,
    string? TimeZoneId = null);

/// <summary>Body of <c>POST /api/admin/notification-campaigns/audience</c>: the count before anything is sent.</summary>
public record CampaignAudienceRequest(string Purpose, CampaignAudienceFilter? Audience = null, IReadOnlyList<string>? Channels = null);

/// <summary>How many people one channel would reach, after their own choices.</summary>
public record CampaignChannelCount(string Channel, int Reachable);

/// <summary>
/// An audience's size. <see cref="Matched"/> pass the filters; <see cref="Reachable"/> of them get the
/// message on at least one chosen channel, which for marketing means they opted in. Starting a campaign
/// confirms <see cref="Reachable"/>.
/// </summary>
public record CampaignAudiencePreviewDto(string Label, int Matched, int Reachable, IReadOnlyList<CampaignChannelCount> Channels);

/// <summary>Body of <c>POST /api/admin/notification-campaigns/preview</c>.</summary>
public record CampaignPreviewRequest(string Purpose, CampaignContent Message, IReadOnlyList<string>? Channels = null);

/// <summary>
/// The message as one channel shows it to the administrator, with their own name in the placeholders.
/// <see cref="Body"/> is the plain text that channel carries; <see cref="Buttons"/> are its button labels
/// or links; <see cref="Footer"/> is a line the channel adds, such as an email's opt-out link.
/// </summary>
public record CampaignChannelPreview(string Channel, string Title, string? Body, string? ImageUrl, IReadOnlyList<string> Buttons, string? Footer);

/// <summary>Each chosen channel's preview, and problems to fix before sending, such as an unknown placeholder.</summary>
public record CampaignPreviewDto(IReadOnlyList<CampaignChannelPreview> Channels, IReadOnlyList<string> Warnings);

/// <summary>Body of <c>POST /{id}/activate</c>: the audience count the administrator saw. A different count now is a 409.</summary>
public record ActivateNotificationCampaignRequest(int AudienceCount);

/// <summary>One channel of a delivery report. <see cref="Opened"/> is null where the channel does not report it.</summary>
public record CampaignChannelReport(string Channel, int Queued, int Sent, int Failed, int Skipped, int? Opened);

/// <summary>The report's totals over every channel.</summary>
public record CampaignReportTotals(int Queued, int Sent, int Failed, int Skipped, int Opened);

/// <summary>What a test send queued to the administrator, and the channels it could not use with the reason.</summary>
public record CampaignTestResultDto(IReadOnlyList<string> Queued, IReadOnlyList<CampaignSkippedChannel> Skipped);

public record CampaignSkippedChannel(string Channel, string Reason);

/// <summary>
/// A campaign or lifecycle message. <see cref="Status"/> is draft, active, paused or cancelled;
/// <see cref="Phase"/> is what the list shows: draft, scheduled, live (an event rule), sent, paused or cancelled.
/// </summary>
public record NotificationCampaignDto(
    Guid Id,
    string Name,
    string Purpose,
    string Kind,
    string Status,
    string Phase,
    CampaignContent Message,
    IReadOnlyList<string> Channels,
    CampaignAudienceFilter Audience,
    string AudienceLabel,
    CampaignTiming Timing,
    string Summary,
    string TimeZoneId,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    int? ConfirmedAudience,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ActivatedAt,
    string? CreatedByName,
    CampaignReportTotals Totals,
    IReadOnlyList<CampaignChannelReport> Report);

/// <summary>Body of <c>POST /api/Notifications/opt-out</c>: the token from a marketing message's link.</summary>
public record MarketingOptOutRequest(string Token);

/// <summary>The kind that was turned off on every channel, in words.</summary>
public record MarketingOptOutDto(string Kind, string Label);
