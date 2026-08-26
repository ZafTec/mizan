namespace Mizan.Telegram;

public class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    /// <summary>From BotFather. Empty means the bot does not start, and the API still does.</summary>
    public string? BotToken { get; set; }

    /// <summary>
    /// Long polling in development, webhook in production. One switch, because
    /// the handler above it does not care which one delivered the update.
    /// </summary>
    public bool UseWebhook { get; set; }

    /// <summary>
    /// Checked against X-Telegram-Bot-Api-Secret-Token on every webhook POST.
    /// The webhook path is the only thing about this service that is public,
    /// so this is the whole of its front door.
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Where the API lives on the internal network.</summary>
    public string ApiUrl { get; set; } = "http://mizan-backend:8080";

    /// <summary>The MCP-tier service key. Internal only; never leaves the Docker network.</summary>
    public string? ServiceApiKey { get; set; }

    /// <summary>The web app, for the "sign in first" message and the settings link.</summary>
    public string PublicUrl { get; set; } = "http://localhost:3000";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken)
        && !string.IsNullOrWhiteSpace(ServiceApiKey);
}
