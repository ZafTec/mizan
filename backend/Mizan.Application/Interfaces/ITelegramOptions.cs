namespace Mizan.Application.Interfaces;

/// <summary>
/// What the API needs to know about the bot: its username, so it can hand the user a working deep
/// link, and how long a Mini App session lasts. Read per use, because an administrator changes both
/// at runtime (docs/ADMIN.md#runtime-settings).
/// </summary>
/// <param name="BotUsername">The bot's @name without the @, or null when no bot is configured.</param>
/// <param name="MiniAppSessionLifetime">
/// How long a session opened from the Mini App lives. Shorter than a browser sign-in: the page is
/// opened from a chat that can open it again.
/// </param>
public sealed record TelegramSettingsSnapshot(string? BotUsername, TimeSpan MiniAppSessionLifetime)
{
    public bool IsConfigured => BotUsername is not null;
}

public interface ITelegramSettings
{
    Task<TelegramSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);
}
