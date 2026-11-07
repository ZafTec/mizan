using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Identity;

/// <summary>
/// The Telegram runtime settings (docs/ADMIN.md#runtime-settings). The API reads the username, the
/// session length and the bot service URL; the rest it only serves to the bot service, which alone
/// calls Telegram (docs/TELEGRAM.md).
/// </summary>
public class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>The bot's @name without the @. Empty disables the feature.</summary>
    public string? BotUsername { get; set; }

    /// <summary>How long a Mini App session lives, in hours. A day by default.</summary>
    public int MiniAppSessionHours { get; set; } = 24;

    /// <summary>Where the API reaches the bot service's <c>/internal/*</c>. Empty sends reminders in the app only.</summary>
    public string? BotServiceUrl { get; set; }

    public string? BotToken { get; set; }

    public bool UseWebhook { get; set; }

    public string? WebhookSecret { get; set; }

    public string? MiniAppUrl { get; set; }

    public int MiniAppMaxAgeSeconds { get; set; } = 300;
}

public sealed class TelegramSettings : ITelegramSettings
{
    private readonly IRuntimeOptions<TelegramOptions> _options;

    public TelegramSettings(IRuntimeOptions<TelegramOptions> options) => _options = options;

    public async Task<TelegramSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var options = await _options.GetAsync(cancellationToken);
        return new TelegramSettingsSnapshot(
            string.IsNullOrWhiteSpace(options.BotUsername) ? null : options.BotUsername.Trim().TrimStart('@'),
            TimeSpan.FromHours(options.MiniAppSessionHours > 0 ? options.MiniAppSessionHours : 24));
    }
}
