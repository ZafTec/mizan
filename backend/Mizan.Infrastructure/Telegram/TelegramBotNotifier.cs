using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Telegram;
using Mizan.Infrastructure.Identity;

namespace Mizan.Infrastructure.Telegram;

/// <summary>How a send to the bot service ended.</summary>
public enum TelegramNotifyOutcome
{
    Sent,

    /// <summary>The bot service or Telegram did not take it; worth retrying.</summary>
    Failed,

    /// <summary>The bot service refused the request itself (a bad key or body); retrying will not help.</summary>
    Refused,
}

/// <summary>The bot service's <c>POST /internal/notify</c>. A seam so tests do not need a bot.</summary>
public interface ITelegramBotNotifier
{
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    Task<TelegramNotifyOutcome> SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Calls the bot service over the internal network with the same service key
/// the bot sends to us, so neither side holds a second secret, and the bot
/// token stays in the bot service. The base address is
/// <c>Telegram:BotServiceUrl</c>, which the admin area can override and which is read per send;
/// without it nothing is sent to Telegram. The key stays in configuration.
/// </summary>
public sealed class TelegramBotNotifier : ITelegramBotNotifier
{
    public const string HttpClientName = "telegram-bot";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRuntimeOptions<TelegramOptions> _settings;
    private readonly string? _serviceKey;

    public TelegramBotNotifier(
        IHttpClientFactory httpClientFactory, IRuntimeOptions<TelegramOptions> settings, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _serviceKey = configuration["Mcp:ServiceApiKey"];
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) =>
        !string.IsNullOrWhiteSpace((await _settings.GetAsync(cancellationToken)).BotServiceUrl)
        && !string.IsNullOrWhiteSpace(_serviceKey);

    public async Task<TelegramNotifyOutcome> SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken)
    {
        var botUrl = (await _settings.GetAsync(cancellationToken)).BotServiceUrl;
        if (string.IsNullOrWhiteSpace(botUrl) || string.IsNullOrWhiteSpace(_serviceKey)) return TelegramNotifyOutcome.Refused;

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(botUrl.TrimEnd('/') + "/"), "internal/notify"))
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Add("X-Api-Key", _serviceKey);

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode) return TelegramNotifyOutcome.Sent;
            return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                ? TelegramNotifyOutcome.Refused
                : TelegramNotifyOutcome.Failed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return TelegramNotifyOutcome.Failed;
        }
    }
}
