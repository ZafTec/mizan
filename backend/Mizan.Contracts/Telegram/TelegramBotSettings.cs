namespace Mizan.Contracts.Telegram;

/// <summary>
/// What the bot service reads from GET /api/internal/settings/telegram-bot at startup, on the
/// settings-changed signal and every few minutes. These are runtime settings an administrator
/// changes in the admin area (docs/TELEGRAM.md#configure-and-run). Only the service key reads them,
/// because the token and the webhook secret travel in clear inside the internal network.
/// </summary>
/// <param name="BotToken">The BotFather token, or null when the bot is off.</param>
/// <param name="UseWebhook">True receives updates at the webhook; false polls Telegram.</param>
/// <param name="WebhookSecret">The secret Telegram echoes with every webhook update.</param>
/// <param name="MiniAppUrl">The Mini App's public origin, or null for the app's own URL.</param>
/// <param name="MiniAppMaxAgeSeconds">How old a Mini App launch may be.</param>
public sealed record TelegramBotSettings(
    string? BotToken,
    string? BotUsername,
    bool UseWebhook,
    string? WebhookSecret,
    string? MiniAppUrl,
    int MiniAppMaxAgeSeconds);
