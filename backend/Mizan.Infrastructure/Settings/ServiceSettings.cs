using Mizan.Application.Interfaces;
using Mizan.Contracts.Prices;
using Mizan.Contracts.Telegram;
using Mizan.Infrastructure.Identity;

namespace Mizan.Infrastructure.Settings;

/// <summary>The prices worker's crawl settings (docs/PLANNING.md#prices).</summary>
public class PriceCrawlOptions
{
    public const string SectionName = "Prices";

    public string UserAgent { get; set; } = "MizanPriceBot/1.0 (+https://mizan.zaftech.co; prices@zaftech.co)";

    public int RequestDelayMs { get; set; } = 1000;

    public int MaxProductsPerRun { get; set; } = 500;
}

internal sealed class ServiceSettings : IServiceSettings
{
    private readonly IRuntimeOptions<TelegramOptions> _telegram;
    private readonly IRuntimeOptions<PriceCrawlOptions> _prices;

    public ServiceSettings(IRuntimeOptions<TelegramOptions> telegram, IRuntimeOptions<PriceCrawlOptions> prices)
    {
        _telegram = telegram;
        _prices = prices;
    }

    public async Task<TelegramBotSettings> GetTelegramBotAsync(CancellationToken cancellationToken = default)
    {
        var options = await _telegram.GetAsync(cancellationToken);
        return new TelegramBotSettings(
            Blank(options.BotToken),
            Blank(options.BotUsername)?.TrimStart('@'),
            options.UseWebhook,
            Blank(options.WebhookSecret),
            Blank(options.MiniAppUrl),
            options.MiniAppMaxAgeSeconds);
    }

    public async Task<PriceCrawlSettings> GetPriceCrawlAsync(CancellationToken cancellationToken = default)
    {
        var options = await _prices.GetAsync(cancellationToken);
        return new PriceCrawlSettings(options.UserAgent, options.RequestDelayMs, options.MaxProductsPerRun);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
