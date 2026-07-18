using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Mizan.Mcp.Server.Services;
using Serilog;

namespace Mizan.Mcp.Server.Authentication;

public class McpTokenAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "McpToken";
}

public class McpTokenAuthenticationHandler : AuthenticationHandler<McpTokenAuthenticationOptions>
{
    private readonly IBackendApiClient _backend;
    private readonly IMemoryCache _cache;

    public McpTokenAuthenticationHandler(IOptionsMonitor<McpTokenAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        IBackendApiClient backend, IMemoryCache? cache = null) : base(options, logger, encoder) { _backend = backend; _cache = cache ?? new MemoryCache(new MemoryCacheOptions()); }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken();
        if (string.IsNullOrEmpty(token)) return AuthenticateResult.NoResult();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        if (!_cache.TryGetValue<TokenValidation>(hash, out var validation))
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            validation = await _backend.ValidateTokenAsync(token, cts.Token);
            if (validation is not null && !validation.MonthlyLimit.HasValue)
            {
                _cache.Set(hash, validation, TimeSpan.FromSeconds(60));
            }
        }
        if (validation is null)
        {
            Log.Warning("[MCP Auth] Token validation failed for hash {TokenHashPrefix}", hash[..12]);
            return AuthenticateResult.Fail("Invalid or expired MCP token");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, validation.UserId.ToString()), new("sub", validation.UserId.ToString()),
            new("mcp_token_id", validation.TokenId.ToString()), new("type", "mcp_token"),
            new(ClaimTypes.Role, validation.Role), new("role", validation.Role), new("plan", validation.Plan),
            new("mcp_usage_used", validation.UsedThisMonth.ToString())
        };
        if (validation.MonthlyLimit.HasValue) claims.Add(new Claim("mcp_usage_limit", validation.MonthlyLimit.Value.ToString()));
        if (validation.RemainingThisMonth.HasValue) claims.Add(new Claim("mcp_usage_remaining", validation.RemainingThisMonth.Value.ToString()));
        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private string? ExtractToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return authHeader["Bearer ".Length..].Trim();
        return Request.Query.TryGetValue("token", out var queryToken) && !string.IsNullOrEmpty(queryToken) ? queryToken.ToString() : null;
    }
}
