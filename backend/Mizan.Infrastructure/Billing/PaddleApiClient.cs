using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Billing;

public class PaddleApiClient : IPaddleApiClient
{
    public const string HttpClientName = "paddle-api";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaddleOptions _options;
    private readonly ILogger<PaddleApiClient> _logger;

    public PaddleApiClient(
        IHttpClientFactory httpClientFactory, IOptions<PaddleOptions> options, ILogger<PaddleApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PaddlePortalSession?> CreatePortalSessionAsync(
        string customerId, string? subscriptionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(BaseUrl());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        object body = subscriptionId is null
            ? new { }
            : new { subscription_ids = new[] { subscriptionId } };

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/customers/{customerId}/portal-sessions", body, Json, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Never the customer id or the response body: both are billing
                // identifiers a log line has no business holding.
                _logger.LogWarning("Paddle portal session request failed with {Status}", (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<PortalSessionResponse>(Json, cancellationToken);
            var overview = payload?.Data?.Urls?.General?.Overview;
            if (string.IsNullOrWhiteSpace(overview))
            {
                return null;
            }

            var subscription = payload!.Data!.Urls!.Subscriptions?.FirstOrDefault();

            return new PaddlePortalSession(
                overview,
                subscription?.CancelSubscription,
                subscription?.UpdateSubscriptionPaymentMethod);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Paddle portal session request could not reach Paddle");
            return null;
        }
    }

    private string BaseUrl() => _options.Environment.Equals("production", StringComparison.OrdinalIgnoreCase)
        ? "https://api.paddle.com"
        : "https://sandbox-api.paddle.com";

    private sealed record PortalSessionResponse
    {
        [JsonPropertyName("data")] public PortalSessionData? Data { get; init; }
    }

    private sealed record PortalSessionData
    {
        [JsonPropertyName("urls")] public PortalUrls? Urls { get; init; }
    }

    private sealed record PortalUrls
    {
        [JsonPropertyName("general")] public GeneralUrls? General { get; init; }
        [JsonPropertyName("subscriptions")] public List<SubscriptionUrls>? Subscriptions { get; init; }
    }

    private sealed record GeneralUrls
    {
        [JsonPropertyName("overview")] public string? Overview { get; init; }
    }

    private sealed record SubscriptionUrls
    {
        [JsonPropertyName("cancel_subscription")] public string? CancelSubscription { get; init; }
        [JsonPropertyName("update_subscription_payment_method")] public string? UpdateSubscriptionPaymentMethod { get; init; }
    }
}
