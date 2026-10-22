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
