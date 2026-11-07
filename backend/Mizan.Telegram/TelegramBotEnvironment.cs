using Mizan.Contracts.Telegram;

namespace Mizan.Telegram;

/// <summary>
/// The bot's environment: the <c>TelegramBot</c> section, and only what it needs before it can reach the API.
/// Everything else about the bot (its token, the webhook switch and secret, the Mini App) is a runtime setting
/// in the admin area, which the bot reads from the API (docs/TELEGRAM.md#configure-and-run).
/// </summary>
public sealed class TelegramBotEnvironment
{
    public const string SectionName = "TelegramBot";

    /// <summary>Keys this section held before they became runtime settings. Present ones are ignored, with a warning.</summary>
    public static readonly string[] RetiredKeys = ["BotToken", "UseWebhook", "WebhookSecret", "MiniAppUrl", "MiniAppMaxAge"];

    /// <summary>Where the API lives on the internal network.</summary>
    public string ApiUrl { get; set; } = "http://mizan-backend:8080";

    /// <summary>The MCP-tier service key. Internal only; never leaves the Docker network.</summary>
    public string? ServiceApiKey { get; set; }

    /// <summary>The web app, for the "sign in first" message, the settings link and the webhook address.</summary>
    public string PublicUrl { get; set; } = "http://localhost:3000";

    /// <summary>See <see cref="TelegramBotOptions.TrustedProxies"/>. Read once at startup by the forwarded headers middleware.</summary>
    public string? TrustedProxies { get; set; }

    /// <summary>The settings the bot works with: this environment plus what the API answered, if anything yet.</summary>
    public TelegramBotOptions With(TelegramBotSettings? remote) => new()
    {
        ApiUrl = ApiUrl,
        ServiceApiKey = ServiceApiKey,
        PublicUrl = PublicUrl,
        TrustedProxies = TrustedProxies,
        BotToken = remote?.BotToken,
        UseWebhook = remote?.UseWebhook ?? false,
        WebhookSecret = remote?.WebhookSecret,
        MiniAppUrl = remote?.MiniAppUrl,
        MiniAppMaxAge = TimeSpan.FromSeconds(remote?.MiniAppMaxAgeSeconds is > 0 and var seconds ? seconds : 300),
    };
}
