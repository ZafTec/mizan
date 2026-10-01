using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Mcp;
using Mizan.Mcp.Server.Services;
using Serilog;

namespace Mizan.Mcp.Server.Authentication;

public class McpOAuthAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "MizanOAuth";
}

/// <summary>
/// Authenticates an OAuth access token by asking the API what it is. Tokens are
/// opaque, so only the API can say. A request without a token is not an error
/// here: the MCP challenge scheme answers it with 401 and where to sign in.
/// </summary>
public class McpOAuthAuthenticationHandler : AuthenticationHandler<McpOAuthAuthenticationOptions>
{
    private static readonly TimeSpan ActiveFor = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InactiveFor = TimeSpan.FromSeconds(5);

    private readonly IBackendApiClient _backend;
    private readonly IMemoryCache _cache;

    public McpOAuthAuthenticationHandler(
        IOptionsMonitor<McpOAuthAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        IBackendApiClient backend, IMemoryCache cache) : base(options, logger, encoder)
    {
        _backend = backend;
        _cache = cache;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

        var token = header["Bearer ".Length..].Trim();
        if (token.Length == 0) return AuthenticateResult.NoResult();

        var key = "mcp-token:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        if (!_cache.TryGetValue<TokenIntrospection>(key, out var introspection))
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Context.RequestAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            introspection = await _backend.IntrospectAsync(token, cts.Token) ?? TokenIntrospection.Inactive;

            // A plan with a monthly call cap is counted on every call, so it is not cached.
            if (!introspection.Active) _cache.Set(key, introspection, InactiveFor);
            else if (introspection.MonthlyLimit is null) _cache.Set(key, introspection, ActiveFor);
        }

        if (!introspection!.Active)
        {
            Log.Warning("[MCP Auth] Access token rejected");
            return AuthenticateResult.Fail("The access token is not valid");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, introspection.UserId.ToString()),
            new("sub", introspection.UserId.ToString()),
            new(ClaimTypes.Role, introspection.Role),
            new("role", introspection.Role),
            new("plan", introspection.Plan),
            new("type", "mcp_oauth"),
            new(GrantClaims.GrantId, introspection.GrantId.ToString()),
            new(GrantClaims.Client, introspection.ClientRowId.ToString()),
            new(GrantClaims.Scopes, string.Join(' ', introspection.Scopes)),
            new(GrantClaims.HouseholdMode, introspection.HouseholdMode),
            new(GrantClaims.Households, string.Join(',', introspection.HouseholdIds)),
            new("mcp_client_name", introspection.ClientName),
            new("mcp_usage_used", introspection.UsedThisMonth.ToString()),
        };
        if (introspection.MonthlyLimit is { } limit) claims.Add(new Claim("mcp_usage_limit", limit.ToString()));

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
