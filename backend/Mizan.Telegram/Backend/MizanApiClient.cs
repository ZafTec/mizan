using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Measurements;

namespace Mizan.Telegram.Backend;

/// <summary>
/// The API, as this service's only source of truth.
///
/// Same pattern as <c>Mizan.Mcp.Server</c>: a service key plus
/// <c>X-Impersonate-User</c>, over the internal network, never exposed. The
/// bot holds no data of its own - no logs, no nutrition, no AI configuration.
/// If it looks like it is deciding something the website also decides, that is
/// the bug (docs/REFOCUS.md §13).
/// </summary>
public sealed class MizanApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<MizanApiClient> _logger;
    private readonly string _serviceKey;

    public MizanApiClient(HttpClient http, IOptions<TelegramBotOptions> options, ILogger<MizanApiClient> logger)
    {
        _http = http;
        _logger = logger;
        _serviceKey = options.Value.ServiceApiKey ?? string.Empty;
        _http.BaseAddress = new Uri(options.Value.ApiUrl.TrimEnd('/') + "/");
    }

    // ---- Linking ----------------------------------------------------------

    public async Task<ResolvedUser?> ResolveAsync(long telegramUserId, CancellationToken ct)
    {
        using var request = Service(HttpMethod.Get, $"api/Telegram/resolve/{telegramUserId}");
        using var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Resolve returned {Status}", (int)response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ResolvedUser>(Json, ct);
    }

    public async Task<LinkOutcome> LinkAsync(
        string code, long telegramUserId, string? username, CancellationToken ct)
    {
        using var request = Service(HttpMethod.Post, "api/Telegram/resolve");
        request.Content = JsonContent.Create(
            new { code, telegramUserId, telegramUsername = username }, options: Json);

        using var response = await _http.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<LinkResult>(Json, ct);
            return new LinkOutcome(true, result?.Name, null);
        }

        // A bad code is the common case, not an error worth a stack trace: the
        // user waited too long, or pasted an old link.
        var problem = await ReadProblemAsync(response, ct);
        return new LinkOutcome(false, null, problem);
    }

    public async Task<bool> UnlinkAsync(long telegramUserId, CancellationToken ct)
    {
        using var request = Service(HttpMethod.Delete, $"api/Telegram/resolve/{telegramUserId}");
        using var response = await _http.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    // ---- Acting as the linked user ----------------------------------------

    public Task<TResponse?> GetAsAsync<TResponse>(Guid userId, string path, CancellationToken ct) =>
        SendAsAsync<TResponse>(userId, HttpMethod.Get, path, null, ct);

    public Task<TResponse?> PostAsAsync<TResponse>(
        Guid userId, string path, object? body, CancellationToken ct) =>
        SendAsAsync<TResponse>(userId, HttpMethod.Post, path, body, ct);

    /// <summary>
    /// Multipart, for the one endpoint that takes a photo. Its own method
    /// rather than a content parameter on the general one, because everything
    /// else here is JSON and blending the two produces a signature nobody can
    /// read.
    /// </summary>
    public async Task<ApiResult<TResponse>> PostImageAsAsync<TResponse>(
        Guid userId, string path, string field, byte[] image, string fileName, CancellationToken ct)
    {
        using var request = Impersonating(HttpMethod.Post, path, userId);

        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(image);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(part, field, fileName);
        request.Content = form;

        using var response = await _http.SendAsync(request, ct);
        return await ReadAsync<TResponse>(response, ct);
    }

    public Task<ApiResult<object>> LogMeasurementAsync(
        Guid userId, LogMeasurementRequest measurement, CancellationToken ct) =>
        SendForResultAsync<object>(userId, HttpMethod.Post, "api/BodyMeasurements", measurement, ct);

    public Task<ApiResult<TResponse>> SendForResultAsync<TResponse>(
        Guid userId, HttpMethod method, string path, object? body, CancellationToken ct) =>
        SendCoreAsync<TResponse>(userId, method, path, body, ct);

    private async Task<TResponse?> SendAsAsync<TResponse>(
        Guid userId, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var result = await SendCoreAsync<TResponse>(userId, method, path, body, ct);
        return result.Value;
    }

    private async Task<ApiResult<TResponse>> SendCoreAsync<TResponse>(
        Guid userId, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = Impersonating(method, path, userId);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        using var response = await _http.SendAsync(request, ct);
        return await ReadAsync<TResponse>(response, ct);
    }

    private async Task<ApiResult<TResponse>> ReadAsync<TResponse>(
        HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = response.StatusCode == HttpStatusCode.NoContent
                ? default
                : await response.Content.ReadFromJsonAsync<TResponse>(Json, ct);
            return new ApiResult<TResponse>(true, value, null, response.StatusCode);
        }

        _logger.LogWarning("API responded {Status}", (int)response.StatusCode);
        return new ApiResult<TResponse>(
            false, default, await ReadProblemAsync(response, ct), response.StatusCode);
    }

    private static async Task<string?> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;

            foreach (var name in new[] { "detail", "title", "error" })
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Not a problem document. The status code is what the caller gets.
        }

        return null;
    }

    private HttpRequestMessage Service(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Api-Key", _serviceKey);
        return request;
    }

    private HttpRequestMessage Impersonating(HttpMethod method, string path, Guid userId)
    {
        var request = Service(method, path);
        request.Headers.Add("X-Impersonate-User", userId.ToString());
        return request;
    }
}

public sealed record ResolvedUser(Guid UserId, string? Name, DateTime LinkedAt);

public sealed record LinkResult(Guid UserId, string? Name);

public sealed record LinkOutcome(bool Linked, string? Name, string? Problem);

public sealed record ApiResult<T>(bool Ok, T? Value, string? Problem, HttpStatusCode Status);
