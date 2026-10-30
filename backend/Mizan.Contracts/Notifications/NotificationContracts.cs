namespace Mizan.Contracts.Notifications;

/// <summary>A button under a notification: its label and the app path it opens, such as <c>/achievements</c>.</summary>
public record NotificationAction(string Label, string Path);

/// <summary>A short label and value shown with a notification, such as "Streak" and "12 days".</summary>
public record NotificationFact(string Label, string Value);

/// <summary>
/// One notification's content, built once in Application and rendered by each channel in its own way
/// (docs/ARCHITECTURE.md#notifications): the web panel as a row, Telegram as an HTML card or a photo
/// with a caption, push and email as each allows.
/// </summary>
/// <param name="Kind">The group the person sets channels for: achievements, reminders, planning, coaching, household, social, account or marketing.</param>
/// <param name="ImageUrl">An absolute image URL that is the content, such as an achievement's badge.</param>
/// <param name="Actions">Up to <see cref="MaxActions"/> buttons.</param>
public record NotificationMessage(
    string Kind,
    string Title,
    string? Body = null,
    string? ImageUrl = null,
    IReadOnlyList<NotificationAction>? Actions = null,
    IReadOnlyList<NotificationFact>? Facts = null)
{
    public const int MaxActions = 2;
}

/// <summary>A notification in the person's list.</summary>
public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    string? LinkUrl,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string Kind,
    string? ImageUrl,
    IReadOnlyList<NotificationAction> Actions,
    IReadOnlyList<NotificationFact> Facts);

public record NotificationListResult(IReadOnlyList<NotificationDto> Items, int UnreadCount, int TotalCount, int Page, int PageSize);

/// <summary>One kind's channels, with the wording to show for it.</summary>
public record NotificationKindPreferenceDto(
    string Kind,
    string Label,
    string Description,
    bool InApp,
    bool Telegram,
    bool Push,
    bool Email);

/// <summary>
/// What <c>GET /api/Notifications/preferences</c> answers. Quiet hours are <c>HH:mm</c> on the person's
/// clock, both null when there are none. Telegram delivery also needs <see cref="TelegramEnabled"/>,
/// the account-wide switch, which needs a linked chat.
/// </summary>
public record NotificationPreferencesDto(
    IReadOnlyList<NotificationKindPreferenceDto> Kinds,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    string TimeZoneId,
    bool TelegramLinked,
    bool TelegramEnabled);

/// <summary>A change to one kind's channels; a null channel is left as it is.</summary>
public record NotificationKindPreferenceUpdate(string Kind, bool? InApp = null, bool? Telegram = null, bool? Push = null, bool? Email = null);

/// <summary>
/// Body of <c>PUT /api/Notifications/preferences</c>. Kinds not listed are left alone. Quiet hours are
/// set with both <c>HH:mm</c> values, and removed with <see cref="ClearQuietHours"/>; leaving all three
/// out keeps them.
/// </summary>
public record UpdateNotificationPreferencesRequest(
    IReadOnlyList<NotificationKindPreferenceUpdate>? Kinds = null,
    string? QuietHoursStart = null,
    string? QuietHoursEnd = null,
    bool? ClearQuietHours = null);
