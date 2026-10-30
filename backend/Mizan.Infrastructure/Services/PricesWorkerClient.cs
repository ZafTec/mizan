using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Services;

/// <summary>
/// Calls the prices worker's <c>POST /run/{sourceId}</c> with the same service
/// key the worker sends to us, so neither side holds a second secret, and with
/// the crawl settings as they stand in the body. The base address is
/// <c>Prices:WorkerUrl</c>, an internal URL that stays in the environment;
/// without it Run now is off.
/// </summary>
public sealed class PricesWorkerClient : IPricesWorkerClient
{
    public const string HttpClientName = "prices-worker";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceSettings _settings;
    private readonly ILogger<PricesWorkerClient> _logger;
    private readonly string? _workerUrl;
    private readonly string? _serviceKey;

    public PricesWorkerClient(
        IHttpClientFactory httpClientFactory, IServiceSettings settings, IConfiguration configuration, ILogger<PricesWorkerClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
        _workerUrl = configuration["Prices:WorkerUrl"];
        _serviceKey = configuration["Mcp:ServiceApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_workerUrl) && !string.IsNullOrWhiteSpace(_serviceKey);

    public async Task<string> RunAsync(Guid sourceId, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_workerUrl!.TrimEnd('/') + "/"), $"run/{sourceId}"));
        request.Headers.Add("X-Api-Key", _serviceKey);
        request.Content = JsonContent.Create(await _settings.GetPriceCrawlAsync(cancellationToken));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "The prices worker did not answer a run request for {SourceId}", sourceId);
            throw new UpstreamUnavailableException("The prices worker did not answer. Check that it is running.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ReadString(body, "status") ?? "started";
            }

            var message = ReadString(body, "error") ?? ReadString(body, "detail") ?? $"The prices worker answered {(int)response.StatusCode}.";
            _logger.LogWarning("The prices worker refused a run of {SourceId}: {Status} {Message}", sourceId, (int)response.StatusCode, message);
            throw response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest
                ? new DomainValidationException(message)
                : new UpstreamUnavailableException(message);
        }
    }

    private static string? ReadString(string body, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
