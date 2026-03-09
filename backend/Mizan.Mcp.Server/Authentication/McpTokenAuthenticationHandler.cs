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
        var token = ExtractToken();
        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        var validation = await _backend.ValidateTokenAsync(token, Context.RequestAborted);
        if (validation == null)
            return AuthenticateResult.Fail("Invalid or expired MCP token");

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
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader["Bearer ".Length..].Trim();

        if (Request.Query.TryGetValue("token", out var queryToken) && !string.IsNullOrEmpty(queryToken))
            return queryToken.ToString();

        return null;
    }
}
