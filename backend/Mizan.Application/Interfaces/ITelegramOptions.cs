namespace Mizan.Application.Interfaces;

/// <summary>
/// What the API needs to know about the bot: its username, so it can hand the
/// user a working deep link. Nothing else - the token lives in the bot service
/// and the API never talks to Telegram.
/// </summary>
public interface ITelegramSettings
{
    /// <summary>The bot's @name without the @, or null when no bot is configured.</summary>
    string? BotUsername { get; }

    bool IsConfigured { get; }
}
