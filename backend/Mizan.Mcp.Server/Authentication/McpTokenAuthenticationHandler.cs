using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Mizan.Mcp.Server.Services;

namespace Mizan.Mcp.Server.Authentication;

public class McpTokenAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "McpToken";
}

public class McpTokenAuthenticationHandler : AuthenticationHandler<McpTokenAuthenticationOptions>
{
    private readonly IBackendClient _backendClient;

    public McpTokenAuthenticationHandler(
        IOptionsMonitor<McpTokenAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IBackendClient backendClient)
        : base(options, logger, encoder)
    {
        _backendClient = backendClient;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken();

        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var validation = await _backendClient.ValidateTokenAsync(token);
        if (validation == null)
        {
            return AuthenticateResult.Fail("Invalid or expired MCP token");
        }

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
        // Try Authorization: Bearer <token> header first
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        // Fall back to ?token= query parameter (for SSE connections that can't set headers)
        if (Request.Query.TryGetValue("token", out var queryToken) && !string.IsNullOrEmpty(queryToken))
        {
            return queryToken.ToString();
        }

        return null;
    }
}
