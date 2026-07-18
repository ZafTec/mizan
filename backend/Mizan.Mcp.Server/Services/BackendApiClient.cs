using System.Net;
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

public sealed record TokenValidation(Guid UserId, Guid TokenId, string Role = "user", string Plan = "free", int? MonthlyLimit = null, int UsedThisMonth = 0, int? RemainingThisMonth = null);

public sealed class BackendApiException : Exception
{
    public HttpStatusCode Status { get; }
    public string? ErrorCode { get; }
    public BackendApiException(HttpStatusCode status, string? errorCode, string message) : base(message) { Status = status; ErrorCode = errorCode; }
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
        _adminServiceApiKey = configuration is null ? _serviceApiKey : configuration["Mcp:AdminServiceApiKey"] ?? configuration["AdminServiceApiKey"] ?? _serviceApiKey;
    }

    private Guid GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var claim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
        return Guid.Parse(claim ?? throw new UnauthorizedAccessException("No user context"));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, object? body = null)
    {
        var request = new HttpRequestMessage(method, endpoint);
        var user = _httpContextAccessor.HttpContext?.User;
        var isAdmin = user?.IsInRole("admin") == true || string.Equals(user?.FindFirst("role")?.Value, "admin", StringComparison.OrdinalIgnoreCase);
        request.Headers.Add("X-Api-Key", isAdmin ? _adminServiceApiKey : _serviceApiKey);
        request.Headers.Add("X-Impersonate-User", GetUserId().ToString());
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
        throw new BackendApiException(response.StatusCode, code, FormatMessage(response.StatusCode, code, message));
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
        HttpStatusCode.Unauthorized => "MCP token is no longer valid. Create a new token in Profile → MCP.",
        HttpStatusCode.Forbidden when code == "upgrade_required" => $"[UPGRADE REQUIRED] {message} Manage plan: https://mizan.euaell.me/billing",
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

    public async Task<TokenValidation?> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/McpTokens/validate") { Content = JsonContent.Create(new { token }) };
            var response = await _http.SendAsync(request, ct); if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<ValidateResponse>(ct);
            return result?.IsValid == true ? new TokenValidation(result.UserId, result.TokenId, result.Role, result.Plan, result.MonthlyLimit, result.UsedThisMonth, result.RemainingThisMonth) : null;
        }
        catch (Exception ex) { _logger.LogError(ex, "Token validation failed"); return null; }
    }

    public async Task LogUsageAsync(Guid tokenId, Guid userId, string toolName, string? parameters, bool success, string? error, int elapsedMs)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/McpTokens/usage")
            { Content = JsonContent.Create(new { McpTokenId = tokenId, ToolName = toolName, Parameters = parameters, Success = success, ErrorMessage = error, ExecutionTimeMs = elapsedMs }) };
            request.Headers.Add("X-Api-Key", _serviceApiKey);
            request.Headers.Add("X-Impersonate-User", userId.ToString()); await _http.SendAsync(request);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to log MCP usage for {Tool}", toolName); }
    }

    private sealed class ValidateResponse
    {
        public Guid UserId { get; set; } public Guid TokenId { get; set; } public bool IsValid { get; set; }
        public string Role { get; set; } = "user";
        public string Plan { get; set; } = "free"; public int? MonthlyLimit { get; set; } public int UsedThisMonth { get; set; } public int? RemainingThisMonth { get; set; }
    }
}
