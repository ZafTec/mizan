using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Identity;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>The bot's @name without the @. Empty disables the feature.</summary>
    public string? BotUsername { get; set; }

    /// <summary>
    /// How long a Mini App session lives (<c>Telegram__MiniAppSessionLifetime</c>,
    /// a TimeSpan such as <c>1.00:00:00</c>). A day by default.
    /// </summary>
    public TimeSpan MiniAppSessionLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Where the API reaches the bot service's <c>/internal/notify</c>. Empty sends reminders in the
    /// app only. An administrator can override it at runtime (docs/ADMIN.md#runtime-settings).
    /// </summary>
    public string? BotServiceUrl { get; set; }
}

/// <summary>
/// The API's whole view of Telegram: a username, so it can build a deep link,
/// and the life of a Mini App session.
///
/// The bot token deliberately does not appear here. The API never calls
/// Telegram - the bot service does, and it is the only thing that holds the
/// token (docs/TELEGRAM.md).
/// </summary>
public class TelegramSettings : ITelegramSettings
{
    public TelegramSettings(IOptions<TelegramOptions> options)
    {
        BotUsername = string.IsNullOrWhiteSpace(options.Value.BotUsername)
            ? null
            : options.Value.BotUsername.TrimStart('@');
        MiniAppSessionLifetime = options.Value.MiniAppSessionLifetime > TimeSpan.Zero
            ? options.Value.MiniAppSessionLifetime
            : TimeSpan.FromHours(24);
    }

    public string? BotUsername { get; }

    public bool IsConfigured => BotUsername is not null;

    public TimeSpan MiniAppSessionLifetime { get; }
}
