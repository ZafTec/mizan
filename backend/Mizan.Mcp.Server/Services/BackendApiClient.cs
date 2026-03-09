using System.Security.Claims;
using System.Text.Json;

namespace Mizan.Mcp.Server.Services;

public interface IBackendApiClient
{
    Task<string> GetAsync(string endpoint, CancellationToken ct = default);
    Task<string> PostAsync(string endpoint, object? body = null, CancellationToken ct = default);
    Task<string> PutAsync(string endpoint, object body, CancellationToken ct = default);
    Task<string> PatchAsync(string endpoint, object body, CancellationToken ct = default);
    Task<string> DeleteAsync(string endpoint, CancellationToken ct = default);
    Task<TokenValidation?> ValidateTokenAsync(string token, CancellationToken ct = default);
    Task LogUsageAsync(Guid tokenId, Guid userId, string toolName, string? parameters, bool success, string? error, int elapsedMs);
}

public sealed record TokenValidation(Guid UserId, Guid TokenId);

public sealed class BackendApiClient : IBackendApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BackendApiClient> _logger;

    public BackendApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor, ILogger<BackendApiClient> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var claim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user?.FindFirst("sub")?.Value;
        return Guid.Parse(claim ?? throw new UnauthorizedAccessException("No user context"));
    }

    private Guid GetTokenId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return Guid.Parse(user?.FindFirst("mcp_token_id")?.Value
                          ?? throw new UnauthorizedAccessException("No token context"));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, object? body = null)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Add("X-Impersonate-User", GetUserId().ToString());

        if (body != null)
            request.Content = JsonContent.Create(body);

        return request;
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await _http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Backend {Status}: {Content}", response.StatusCode, content);
            throw new InvalidOperationException($"API error {(int)response.StatusCode}: {content}");
        }

        return content;
    }

    public Task<string> GetAsync(string endpoint, CancellationToken ct = default)
        => SendAsync(CreateRequest(HttpMethod.Get, endpoint), ct);

    public Task<string> PostAsync(string endpoint, object? body = null, CancellationToken ct = default)
        => SendAsync(CreateRequest(HttpMethod.Post, endpoint, body), ct);

    public Task<string> PutAsync(string endpoint, object body, CancellationToken ct = default)
        => SendAsync(CreateRequest(HttpMethod.Put, endpoint, body), ct);

    public Task<string> PatchAsync(string endpoint, object body, CancellationToken ct = default)
        => SendAsync(CreateRequest(HttpMethod.Patch, endpoint, body), ct);

    public Task<string> DeleteAsync(string endpoint, CancellationToken ct = default)
        => SendAsync(CreateRequest(HttpMethod.Delete, endpoint), ct);

    public async Task<TokenValidation?> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/McpTokens/validate")
            {
                Content = JsonContent.Create(new { token })
            };
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<ValidateResponse>(ct);
            return result?.IsValid == true ? new TokenValidation(result.UserId, result.TokenId) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            return null;
        }
    }

    public async Task LogUsageAsync(Guid tokenId, Guid userId, string toolName, string? parameters, bool success, string? error, int elapsedMs)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/McpTokens/usage")
            {
                Content = JsonContent.Create(new
                {
                    McpTokenId = tokenId,
                    ToolName = toolName,
                    Parameters = parameters,
                    Success = success,
                    ErrorMessage = error,
                    ExecutionTimeMs = elapsedMs
                })
            };
            request.Headers.Add("X-Impersonate-User", userId.ToString());
            await _http.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log MCP usage for {Tool}", toolName);
        }
    }

    private sealed class ValidateResponse
    {
        public Guid UserId { get; set; }
        public Guid TokenId { get; set; }
        public bool IsValid { get; set; }
    }
}
