using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Identity;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>The bot's @name without the @. Empty disables the feature.</summary>
    public string? BotUsername { get; set; }
}

/// <summary>
/// The API's whole view of Telegram: a username, so it can build a deep link.
///
/// The bot token deliberately does not appear here. The API never calls
/// Telegram - the bot service does, and it is the only thing that holds the
/// token (docs/REFOCUS.md §13).
/// </summary>
public class TelegramSettings : ITelegramSettings
{
    public TelegramSettings(IOptions<TelegramOptions> options)
    {
        BotUsername = string.IsNullOrWhiteSpace(options.Value.BotUsername)
            ? null
            : options.Value.BotUsername.TrimStart('@');
    }

    public string? BotUsername { get; }

    public bool IsConfigured => BotUsername is not null;
}
