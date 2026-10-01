using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Mizan.Mcp.Server.Authorization;

namespace Mizan.Mcp.Server.Services;

public interface IBackendApiClient
{
    Task<string> GetAsync(string endpoint, CancellationToken ct = default);
    Task<string> PostAsync(string endpoint, object? body = null, CancellationToken ct = default);
    Task<string> PutAsync(string endpoint, object body, CancellationToken ct = default);
    Task<string> PatchAsync(string endpoint, object body, CancellationToken ct = default);
    Task<string> DeleteAsync(string endpoint, CancellationToken ct = default);

    /// <summary>
    /// A multipart POST, for the two endpoints that take a file. The MCP
    /// server runs server-side, so a local path would mean nothing here -
    /// callers hand over bytes.
    /// </summary>
    Task<string> PostFileAsync(
        string endpoint,
        string fieldName,
        byte[] content,
        string fileName,
        string contentType,
        CancellationToken ct = default);
    /// <summary>Asks the API what an access token is. Null when the API cannot be reached.</summary>
    Task<TokenIntrospection?> IntrospectAsync(string token, CancellationToken ct = default);
    Task LogUsageAsync(Guid grantId, Guid userId, string kind, string name, bool success, string? error, int elapsedMs);
}

/// <summary>What the API says about an access token: who it is for and what it may do.</summary>
public sealed record TokenIntrospection
{
    public bool Active { get; init; }
    public Guid UserId { get; init; }
    public Guid GrantId { get; init; }
    public Guid ClientRowId { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; init; } = [];
    public string HouseholdMode { get; init; } = "none";
    public IReadOnlyList<Guid> HouseholdIds { get; init; } = [];
    public string Role { get; init; } = "user";
    public string Plan { get; init; } = "free";
    public int? MonthlyLimit { get; init; }
    public int UsedThisMonth { get; init; }

    public static TokenIntrospection Inactive { get; } = new();
}

public sealed class BackendApiException : Exception
{
    public HttpStatusCode Status { get; }
    public string? ErrorCode { get; }
    public string? ResponseBody { get; }
    public BackendApiException(HttpStatusCode status, string? errorCode, string message, string? responseBody = null) : base(message)
    { Status = status; ErrorCode = errorCode; ResponseBody = responseBody; }
}

public sealed class BackendApiClient : IBackendApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BackendApiClient> _logger;
    private readonly string _serviceApiKey;
    private readonly string _adminServiceApiKey;

    public BackendApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor, ILogger<BackendApiClient> logger, IConfiguration? configuration = null)
    {
        _http = http; _httpContextAccessor = httpContextAccessor; _logger = logger;
        _serviceApiKey = configuration is null ? "test-api-key" : configuration["Mcp:ServiceApiKey"] ?? configuration["ServiceApiKey"] ?? throw new InvalidOperationException("ServiceApiKey not configured");
        _adminServiceApiKey = configuration is null ? "test-admin-api-key" : configuration["Mcp:AdminServiceApiKey"] ?? configuration["AdminServiceApiKey"] ?? throw new InvalidOperationException("AdminServiceApiKey not configured");
        if (string.Equals(_serviceApiKey, _adminServiceApiKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("MCP service and admin API keys must be different");
        }
    }

    private Guid GetUserId()
    {
        var user = McpCallIdentity.Of(_httpContextAccessor.HttpContext);
        var claim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
        return Guid.Parse(claim ?? throw new UnauthorizedAccessException("No user context"));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, object? body = null)
    {
        var request = new HttpRequestMessage(method, endpoint);
        var user = McpCallIdentity.Of(_httpContextAccessor.HttpContext);
        var isAdmin = user?.IsInRole("admin") == true || string.Equals(user?.FindFirst("role")?.Value, "admin", StringComparison.OrdinalIgnoreCase);
        request.Headers.Add("X-Api-Key", isAdmin ? _adminServiceApiKey : _serviceApiKey);
        request.Headers.Add("X-Impersonate-User", GetUserId().ToString());

        // Names the connection, so the API applies what the user allowed it. The API
        // reads the grant from its own database and never trusts anything in this header.
        if (user?.FindFirst("grant_id")?.Value is { } grantId) request.Headers.Add("X-Mcp-Grant", grantId);
        if (body != null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await _http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (response.IsSuccessStatusCode) return content;
        var (code, message) = ParseError(content, response.StatusCode);
        _logger.LogWarning("Backend request failed with {Status} {ErrorCode}", response.StatusCode, code);
        throw new BackendApiException(response.StatusCode, code, FormatMessage(response.StatusCode, code, message), content);
    }

    private static (string? Code, string Message) ParseError(string content, HttpStatusCode status)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var code = root.TryGetProperty("errorCode", out var codeElement) ? codeElement.GetString() : null;
            var message = root.TryGetProperty("detail", out var detail) ? detail.GetString()
                : root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString()
                : root.TryGetProperty("title", out var title) ? title.GetString() : null;
            if (root.TryGetProperty("errors", out var errors))
            {
                var lines = new List<string>();
                if (errors.ValueKind == JsonValueKind.Array)
                    lines.AddRange(errors.EnumerateArray().Select(item => $"{Read(item, "propertyName")}: {Read(item, "errorMessage")}"));
                else if (errors.ValueKind == JsonValueKind.Object)
                    foreach (var property in errors.EnumerateObject()) lines.Add($"{property.Name}: {string.Join(", ", property.Value.EnumerateArray().Select(v => v.GetString()))}");
                if (lines.Count > 0) message = string.Join(Environment.NewLine, lines);
            }
            return (code, message ?? $"Backend returned {(int)status}");
        }
        catch (JsonException) { return (null, string.IsNullOrWhiteSpace(content) ? $"Backend returned {(int)status}" : content); }
    }

    private static string Read(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject()) if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static string FormatMessage(HttpStatusCode status, string? code, string message) => status switch
    {
        HttpStatusCode.Unauthorized => "This connection is no longer valid. Connect Mizan again from your MCP client.",
        HttpStatusCode.Forbidden when code == "upgrade_required" => $"[UPGRADE REQUIRED] {message} Manage plan: https://mizan.zaftech.co/billing",
        HttpStatusCode.Forbidden => message,
        HttpStatusCode.NotFound => $"Not found: {message}",
        HttpStatusCode.TooManyRequests => $"Rate limited: {message}. Wait before retrying.",
        HttpStatusCode.BadRequest => message,
        _ => message
    };

    public Task<string> GetAsync(string endpoint, CancellationToken ct = default) => SendAsync(CreateRequest(HttpMethod.Get, endpoint), ct);
    public Task<string> PostAsync(string endpoint, object? body = null, CancellationToken ct = default) => SendAsync(CreateRequest(HttpMethod.Post, endpoint, body), ct);
    public Task<string> PutAsync(string endpoint, object body, CancellationToken ct = default) => SendAsync(CreateRequest(HttpMethod.Put, endpoint, body), ct);
    public Task<string> PatchAsync(string endpoint, object body, CancellationToken ct = default) => SendAsync(CreateRequest(HttpMethod.Patch, endpoint, body), ct);
    public Task<string> DeleteAsync(string endpoint, CancellationToken ct = default) => SendAsync(CreateRequest(HttpMethod.Delete, endpoint), ct);

    public Task<string> PostFileAsync(
        string endpoint,
        string fieldName,
        byte[] content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        var request = CreateRequest(HttpMethod.Post, endpoint);

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, fieldName, fileName);
        request.Content = form;

        return SendAsync(request, ct);
    }

    public async Task<TokenIntrospection?> IntrospectAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/oauth/introspect")
            {
                Content = JsonContent.Create(new { token, audience = "mcp" })
            };
            request.Headers.Add("X-Api-Key", _serviceApiKey);
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TokenIntrospection>(ct);
        }
        catch (Exception ex) { _logger.LogError(ex, "Token introspection failed"); return null; }
    }

    public async Task LogUsageAsync(Guid grantId, Guid userId, string kind, string name, bool success, string? error, int elapsedMs)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/McpConnections/usage")
            { Content = JsonContent.Create(new { GrantId = grantId, Kind = kind, ToolName = name, Success = success, ErrorMessage = error, ExecutionTimeMs = elapsedMs }) };
            request.Headers.Add("X-Api-Key", _serviceApiKey);
            request.Headers.Add("X-Impersonate-User", userId.ToString());
            await _http.SendAsync(request);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to log MCP usage for {Name}", name); }
    }
}
