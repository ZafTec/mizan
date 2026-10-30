using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Prices;

namespace Mizan.Prices.Backend;

/// <summary>
/// The API as the worker sees it. Faked in tests; <see cref="PricesApiClient"/> is the only implementation that talks HTTP.
/// </summary>
public interface IPricesApi
{
    Task<IReadOnlyList<PriceSourceDto>> GetSourcesAsync(CancellationToken ct);

    /// <summary>The crawl settings an administrator set in the admin area (Prices.*).</summary>
    Task<PriceCrawlSettings> GetCrawlSettingsAsync(CancellationToken ct);

    Task<PriceObservationReceipt> PostObservationsAsync(PriceObservationBatch batch, CancellationToken ct);

    Task PostRunAsync(Guid sourceId, PriceSourceRunResult result, CancellationToken ct);
}

/// <summary>
/// The API, as this service's only source of truth.
///
/// Same pattern as <c>Mizan.Telegram</c>, minus impersonation: the worker acts
/// for no user. It sends the service key in <c>X-Api-Key</c> over the internal
/// network and holds no data of its own - sources, aliases and prices all live
/// behind the API (docs/PLANNING.md).
/// </summary>
public sealed class PricesApiClient : IPricesApi
{
    /// <summary>Observations per POST. Scrapers may emit thousands; the API stores each batch in one transaction.</summary>
    public const int ChunkSize = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _serviceKey;

    public PricesApiClient(HttpClient http, IOptions<PricesOptions> options)
    {
        _http = http;
        _serviceKey = options.Value.ServiceApiKey ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(options.Value.ApiUrl))
        {
            _http.BaseAddress = new Uri(options.Value.ApiUrl.TrimEnd('/') + "/");
        }
    }

    public async Task<IReadOnlyList<PriceSourceDto>> GetSourcesAsync(CancellationToken ct)
    {
        using var request = Service(HttpMethod.Get, "api/internal/prices/sources");
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<PriceSourceDto>>(Json, ct) ?? [];
    }

    public async Task<PriceCrawlSettings> GetCrawlSettingsAsync(CancellationToken ct)
    {
        using var request = Service(HttpMethod.Get, "api/internal/settings/prices");
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PriceCrawlSettings>(Json, ct)
            ?? throw new HttpRequestException("The API answered the crawl settings with an empty body.");
    }

    public async Task<PriceObservationReceipt> PostObservationsAsync(PriceObservationBatch batch, CancellationToken ct)
    {
        using var request = Service(HttpMethod.Post, "api/internal/prices/observations");
        request.Content = JsonContent.Create(batch, options: Json);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PriceObservationReceipt>(Json, ct)
            ?? new PriceObservationReceipt(0, 0, 0);
    }

    public async Task PostRunAsync(Guid sourceId, PriceSourceRunResult result, CancellationToken ct)
    {
        using var request = Service(HttpMethod.Post, $"api/internal/prices/sources/{sourceId}/runs");
        request.Content = JsonContent.Create(result, options: Json);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage Service(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Api-Key", _serviceKey);
        return request;
    }
}
