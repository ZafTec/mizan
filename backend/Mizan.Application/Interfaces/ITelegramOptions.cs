namespace Mizan.Application.Interfaces;

/// <summary>
/// What the API needs to know about the bot: its username, so it can hand the
/// user a working deep link, and how long a Mini App session lasts. Nothing
/// else - the token lives in the bot service and the API never talks to
/// Telegram.
/// </summary>
public interface ITelegramSettings
{
    /// <summary>The bot's @name without the @, or null when no bot is configured.</summary>
    string? BotUsername { get; }

    bool IsConfigured { get; }

    /// <summary>
    /// How long a session opened from the Mini App lives. Shorter than a browser
    /// sign-in: the page is opened from a chat that can open it again.
    /// </summary>
    TimeSpan MiniAppSessionLifetime { get; }
}
