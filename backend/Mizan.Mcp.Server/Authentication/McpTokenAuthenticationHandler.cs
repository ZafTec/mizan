using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
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

    public McpTokenAuthenticationHandler(
        IOptionsMonitor<McpTokenAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IBackendApiClient backend)
        : base(options, logger, encoder)
    {
        _backend = backend;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Log.Debug("[MCP Auth] HandleAuthenticateAsync called for {Method} {Path}", Request.Method, Request.Path);

        var token = ExtractToken();
        if (string.IsNullOrEmpty(token))
        {
            Log.Debug("[MCP Auth] No token found in request");
            return AuthenticateResult.NoResult();
        }

        Log.Debug("[MCP Auth] Token extracted (length: {TokenLength})", token.Length);

        // Don't pass Context.RequestAborted — SSE disconnect cancels it,
        // which kills validation on the subsequent reconnect attempt.
        // 30s matches HttpClient.Timeout — covers cold DB connection pool warmup.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        Log.Debug("[MCP Auth] Validating token with backend...");
        var validation = await _backend.ValidateTokenAsync(token, cts.Token);

        if (validation == null)
        {
            Log.Warning("[MCP Auth] Token validation failed for token: {Token}", token[..Math.Min(10, token.Length)] + "***");
            return AuthenticateResult.Fail("Invalid or expired MCP token");
        }

        Log.Information("[MCP Auth] Token validated successfully. UserId: {UserId}, TokenId: {TokenId}",
            validation.UserId, validation.TokenId);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, validation.UserId.ToString()),
            new Claim("sub", validation.UserId.ToString()),
            new Claim("mcp_token_id", validation.TokenId.ToString()),
            new Claim("type", "mcp_token"),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private string? ExtractToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        Log.Debug("[MCP Auth] Authorization header present: {Present}", !string.IsNullOrEmpty(authHeader));

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("[MCP Auth] Token found in Bearer header");
            return authHeader["Bearer ".Length..].Trim();
        }

        if (Request.Query.TryGetValue("token", out var queryToken) && !string.IsNullOrEmpty(queryToken))
        {
            Log.Debug("[MCP Auth] Token found in query parameter");
            return queryToken.ToString();
        }

        Log.Debug("[MCP Auth] Authorization header: '{Header}'", authHeader);
        Log.Debug("[MCP Auth] Query params: {QueryKeys}", string.Join(", ", Request.Query.Keys));
        return null;
    }
}
