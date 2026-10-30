namespace Mizan.Contracts.Telegram;

/// <summary>
/// Body of <c>POST /api/Telegram/sessions</c>, sent by the bot service with
/// its service key after it has verified Telegram's signed <c>initData</c>.
/// The Telegram id alone is not a claim about who the person is; the bot's
/// signature check is, and only the bot can make it, so only the bot's key
/// is accepted here.
/// </summary>
/// <param name="TelegramUserId">The id from the signed <c>user</c> field.</param>
/// <param name="IpAddress">The WebView's address as the bot saw it, for the sessions screen.</param>
/// <param name="UserAgent">The WebView's user agent, for the sessions screen.</param>
public record TelegramMiniAppSessionRequest(long TelegramUserId, string? IpAddress = null, string? UserAgent = null);

/// <summary>
/// An ordinary Mizan session for the linked user, with a shorter life than a
/// browser sign-in. The token goes into the <c>mizan_session</c> cookie.
/// </summary>
public record TelegramMiniAppSessionResponse(string Token, DateTime ExpiresAt, Guid UserId);

/// <summary>
/// Body of <c>POST /telegram/miniapp/session</c> on the bot service: the raw
/// <c>window.Telegram.WebApp.initData</c> string, unparsed.
/// </summary>
public record MiniAppSessionRequest(string? InitData);

/// <summary>
/// What the bot service answers the Mini App page with on success. The same
/// token is also set as the <c>mizan_session</c> cookie on the response.
/// </summary>
public record MiniAppSessionResponse(string Token, DateTime ExpiresAt);

/// <summary>
/// Body of <c>POST /internal/notify</c> on the bot service, sent by the API's
/// outbox with the shared service key. <see cref="Text"/> is the plain-text
/// form every sender fills; <see cref="Card"/>, when present, is the structured
/// notification the bot renders instead (docs/TELEGRAM.md#notifications-in-telegram).
/// Without a card the bot escapes the text and adds an <b>Open</b> button for
/// <see cref="OpenPath"/> when the Mini App is HTTPS.
/// </summary>
/// <param name="TelegramUserId">The linked Telegram user; in a private chat it is also the chat id.</param>
/// <param name="Text">Plain text, not HTML.</param>
/// <param name="OpenPath">A path under the Mini App base, such as <c>plan?planId=...&amp;day=...</c>.</param>
/// <param name="Card">The structured message, or null for a plain line.</param>
public record TelegramNotifyRequest(long TelegramUserId, string Text, string? OpenPath = null, TelegramNotifyCard? Card = null);

/// <summary>
/// A notification as the bot renders it: a bold title, the body and the facts as short lines, as a
/// photo caption when there is an image, with one inline button per action.
/// </summary>
/// <param name="ImageUrl">A public HTTPS image Telegram fetches itself; when it cannot, the bot sends the text alone.</param>
/// <param name="Callbacks">Buttons the bot answers itself, such as a reminder's <b>Done</b>, <b>Snooze 30 min</b> and <b>Skip today</b>; in one row under the others.</param>
public record TelegramNotifyCard(
    string Title,
    string? Body,
    string? ImageUrl,
    IReadOnlyList<TelegramNotifyButton> Buttons,
    IReadOnlyList<Notifications.NotificationFact> Facts,
    IReadOnlyList<TelegramCallbackButton>? Callbacks = null);

/// <summary>A button whose press comes back to the bot as <see cref="Data"/> (at most 64 bytes).</summary>
public record TelegramCallbackButton(string Label, string Data);

/// <summary>
/// The buttons on a reminder's message (docs/TELEGRAM.md#reminders): <c>ra:done:{id}</c>,
/// <c>ra:snooze:{id}</c> and <c>ra:skip:{id}</c>, with the reminder's id in 32 hex digits so each fits
/// Telegram's 64 bytes. The API writes them and the bot reads them back.
/// </summary>
public static class TelegramReminderCallbacks
{
    public const string Prefix = "ra:";
    public const string Done = "done";
    public const string Snooze = "snooze";
    public const string Skip = "skip";
    public const string Stop = "stop";

    public static string Data(string action, Guid reminderId) => $"{Prefix}{action}:{reminderId:N}";

    public static IReadOnlyList<TelegramCallbackButton> For(Guid reminderId) =>
    [
        new("Done", Data(Done, reminderId)),
        new("Snooze 30 min", Data(Snooze, reminderId)),
        new("Skip today", Data(Skip, reminderId)),
    ];

    public static bool TryParse(string? data, out string action, out Guid reminderId)
    {
        action = string.Empty;
        reminderId = Guid.Empty;
        if (data is null || !data.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var parts = data.Split(':');
        if (parts is not [_, var verb, var id] || verb is not (Done or Snooze or Skip or Stop) || !Guid.TryParseExact(id, "N", out reminderId)) return false;
        action = verb;
        return true;
    }
}

/// <summary>
/// The button on news and offers (docs/TELEGRAM.md#notifications-in-telegram): <c>nm:off</c> turns
/// marketing off on every channel in one tap. The API writes it and the bot answers it.
/// </summary>
public static class TelegramMarketingCallbacks
{
    public const string OptOut = "nm:off";
    public const string Label = "Turn off news and offers";

    public static IReadOnlyList<TelegramCallbackButton> For() => [new(Label, OptOut)];
}

/// <summary>
/// One button. A Mini App page (<see cref="MiniAppPages"/>) opens inside Telegram when the Mini App is
/// HTTPS; otherwise the web path opens the app in the browser when the public address is HTTPS.
/// </summary>
/// <param name="MiniAppPath">A path under the Mini App base, or null when the Mini App has no such page.</param>
/// <param name="WebPath">The app path, such as <c>/achievements</c>.</param>
public record TelegramNotifyButton(string Label, string? MiniAppPath, string WebPath);
