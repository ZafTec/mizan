using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;
using Mizan.Contracts.Prices;
using Mizan.Contracts.Telegram;

namespace Mizan.Application.Settings;

/// <summary>GET /api/internal/settings/telegram-bot.</summary>
public record GetTelegramBotSettingsQuery : IRequest<TelegramBotSettings>;

/// <summary>GET /api/internal/settings/prices.</summary>
public record GetPriceCrawlSettingsQuery : IRequest<PriceCrawlSettings>;

/// <summary>GET /api/internal/settings/mcp-gateway.</summary>
public record GetMcpGatewaySettingsQuery : IRequest<McpGatewaySettings>;

public sealed class GetMcpGatewaySettingsQueryHandler : IRequestHandler<GetMcpGatewaySettingsQuery, McpGatewaySettings>
{
    private readonly IServiceSettings _settings;

    public GetMcpGatewaySettingsQueryHandler(IServiceSettings settings) => _settings = settings;

    public Task<McpGatewaySettings> Handle(GetMcpGatewaySettingsQuery request, CancellationToken cancellationToken) =>
        _settings.GetMcpGatewayAsync(cancellationToken);
}

public sealed class GetTelegramBotSettingsQueryHandler : IRequestHandler<GetTelegramBotSettingsQuery, TelegramBotSettings>
{
    private readonly IServiceSettings _settings;

    public GetTelegramBotSettingsQueryHandler(IServiceSettings settings) => _settings = settings;

    public Task<TelegramBotSettings> Handle(GetTelegramBotSettingsQuery request, CancellationToken cancellationToken) =>
        _settings.GetTelegramBotAsync(cancellationToken);
}

public sealed class GetPriceCrawlSettingsQueryHandler : IRequestHandler<GetPriceCrawlSettingsQuery, PriceCrawlSettings>
{
    private readonly IServiceSettings _settings;

    public GetPriceCrawlSettingsQueryHandler(IServiceSettings settings) => _settings = settings;

    public Task<PriceCrawlSettings> Handle(GetPriceCrawlSettingsQuery request, CancellationToken cancellationToken) =>
        _settings.GetPriceCrawlAsync(cancellationToken);
}
