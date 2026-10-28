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

/// <summary>The bot service's <c>POST /internal/*</c>. A seam so tests do not need a bot.</summary>
public interface ITelegramBotNotifier
{
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    Task<TelegramNotifyOutcome> SendAsync(TelegramNotifyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Tells the bot service a Telegram setting changed, so it reads them again at once. Best effort:
    /// the bot also reads them every few minutes, so a missed signal only delays the change.
    /// </summary>
    Task SettingsChangedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Calls the bot service over the internal network with the same service key
/// the bot sends to us, so neither side holds a second secret. The base address is
/// <c>Telegram.BotServiceUrl</c>, a runtime setting read per send; without it
/// nothing is sent to Telegram. The key stays in configuration.
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
        using var message = await RequestAsync("internal/notify", cancellationToken);
        if (message is null) return TelegramNotifyOutcome.Refused;
        message.Content = JsonContent.Create(request);

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(message, cancellationToken);
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

    public async Task SettingsChangedAsync(CancellationToken cancellationToken = default)
    {
        using var message = await RequestAsync("internal/settings-changed", cancellationToken);
        if (message is null) return;

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The bot's periodic read picks the change up instead.
        }
    }

    private async Task<HttpRequestMessage?> RequestAsync(string path, CancellationToken cancellationToken)
    {
        var botUrl = (await _settings.GetAsync(cancellationToken)).BotServiceUrl;
        if (string.IsNullOrWhiteSpace(botUrl) || string.IsNullOrWhiteSpace(_serviceKey)) return null;

        var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(botUrl.TrimEnd('/') + "/"), path));
        message.Headers.Add("X-Api-Key", _serviceKey);
        return message;
    }
}
