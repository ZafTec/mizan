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

    /// <summary>
    /// Where the Mini App pages live. Empty means <c>{PublicUrl}/tg</c>, which is
    /// where the web app serves them; set it only when the Mini App is hosted
    /// somewhere else. Telegram opens <c>web_app</c> buttons over HTTPS only.
    /// </summary>
    public string? MiniAppUrl { get; set; }

    /// <summary>
    /// How old a signed <c>initData</c> may be and still open a session. Telegram
    /// signs it when the Mini App opens, so a few minutes covers a slow page and
    /// leaves a stolen copy worthless by the time it could be replayed.
    /// </summary>
    public TimeSpan MiniAppMaxAge { get; set; } = TimeSpan.FromMinutes(5);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken)
        && !string.IsNullOrWhiteSpace(ServiceApiKey);

    /// <summary>The Mini App's base URL without a trailing slash.</summary>
    public string MiniAppBaseUrl =>
        string.IsNullOrWhiteSpace(MiniAppUrl) ? $"{PublicUrl.TrimEnd('/')}/tg" : MiniAppUrl.TrimEnd('/');
}
