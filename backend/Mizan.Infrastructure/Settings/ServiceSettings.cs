using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;
using Mizan.Contracts.Prices;
using Mizan.Contracts.Telegram;
using Mizan.Infrastructure.Ai;
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

/// <summary>The assistant gateway's settings, which the MCP server reads (docs/MCP.md#served-to-a-hosted-assistant-through-the-gateway).</summary>
public class GatewaySettingsOptions
{
    public const string SectionName = "Gateway";

    public string ClientKey { get; set; } = string.Empty;

    public string ClientKeyHeader { get; set; } = string.Empty;

    public string MetaKey { get; set; } = string.Empty;

    public string JwksUrl { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string TokenType { get; set; } = string.Empty;
}

internal sealed class ServiceSettings : IServiceSettings
{
    private readonly IRuntimeOptions<TelegramOptions> _telegram;
    private readonly IRuntimeOptions<PriceCrawlOptions> _prices;
    private readonly IRuntimeOptions<GatewaySettingsOptions> _gateway;
    private readonly IRuntimeOptions<AiEndUserOptions> _endUser;

    public ServiceSettings(
        IRuntimeOptions<TelegramOptions> telegram,
        IRuntimeOptions<PriceCrawlOptions> prices,
        IRuntimeOptions<GatewaySettingsOptions> gateway,
        IRuntimeOptions<AiEndUserOptions> endUser)
    {
        _telegram = telegram;
        _prices = prices;
        _gateway = gateway;
        _endUser = endUser;
    }

    public async Task<McpGatewaySettings> GetMcpGatewayAsync(CancellationToken cancellationToken = default)
    {
        var gateway = await _gateway.GetAsync(cancellationToken);
        var endUser = await _endUser.GetAsync(cancellationToken);
        return new McpGatewaySettings(
            Blank(gateway.ClientKey),
            Blank(gateway.ClientKeyHeader),
            Blank(gateway.MetaKey),
            Blank(gateway.JwksUrl),
            Blank(gateway.Issuer),
            Blank(gateway.Audience),
            Blank(gateway.TokenType),
            Blank(endUser.Issuer));
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
