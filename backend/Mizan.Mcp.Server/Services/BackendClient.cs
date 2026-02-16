using System.Net.Http.Headers;
using System.Text.Json;

namespace Mizan.Mcp.Server.Services;

public interface IBackendClient
{
    Task<McpTokenValidation?> ValidateTokenAsync(string token);
    Task<string> CallApiAsync(Guid userId, string method, string endpoint, object? data = null);
    Task LogUsageAsync(Guid mcpTokenId, Guid userId, string toolName, string? parameters, bool success, string? errorMessage, int executionTimeMs);
}

public sealed record McpTokenValidation(Guid UserId, Guid TokenId);

public class BackendClient : IBackendClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<BackendClient> _logger;

    public BackendClient(HttpClient httpClient, IConfiguration configuration, ILogger<BackendClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(configuration["MizanApiUrl"] ?? configuration["BACKEND_API_URL"] ?? "http://mizan-backend:8080");
        
        // Handle potentially namespaced config or env var
        _apiKey = configuration["ServiceApiKey"] 
               ?? configuration["Mcp:ServiceApiKey"] 
               ?? throw new InvalidOperationException("ServiceApiKey not configured. Set 'ServiceApiKey' or 'Mcp__ServiceApiKey'.");
        
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
    }

    public async Task<McpTokenValidation?> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/McpTokens/validate")
            {
                Content = JsonContent.Create(new { token })
            };
            // Validation endpoint is public/anonymous, no API key needed usually, 
            // but the controller allows anonymous.


            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Token validation failed with status {Status}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadFromJsonAsync<ValidateTokenResponse>();
            return content?.IsValid == true ? new McpTokenValidation(content.UserId, content.TokenId) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return null;
        }
    }

    public async Task LogUsageAsync(Guid mcpTokenId, Guid userId, string toolName, string? parameters, bool success, string? errorMessage, int executionTimeMs)
    {
        var payload = new
        {
            McpTokenId = mcpTokenId,
            ToolName = toolName,
            Parameters = parameters,
            Success = success,
            ErrorMessage = errorMessage,
            ExecutionTimeMs = executionTimeMs
        };

        try
        {
            await CallApiAsync(userId, "POST", "/api/McpTokens/usage", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log MCP usage for tool {Tool}", toolName);
        }
    }

    public async Task<string> CallApiAsync(Guid userId, string method, string endpoint, object? data = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        
        // Service Auth Headers
        request.Headers.TryAddWithoutValidation("X-Api-Key", _apiKey);
        request.Headers.Add("X-Impersonate-User", userId.ToString());

        if (data != null)
        {
            request.Content = JsonContent.Create(data);
        }

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Backend API call failed: {Status} {Content}", response.StatusCode, content);
            throw new Exception($"Backend API error: {response.StatusCode} - {content}");
        }

        return content;
    }

    private class ValidateTokenResponse
    {
        public Guid UserId { get; set; }
        public bool IsValid { get; set; }
        public Guid TokenId { get; set; }
    }
}
