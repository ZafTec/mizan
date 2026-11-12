using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Mcp;
using Serilog;

namespace Mizan.Mcp.Server.Authentication;

public sealed class GatewayAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "MizanGateway";

    /// <summary>Picks the gateway or OAuth for each request; the default authenticate scheme.</summary>
    public const string Selector = "MizanMcp";
}

/// <summary>
/// Authenticates the gateway itself by its static key. The principal names no person: who a tool call is for
/// comes from the signed token in that call's <c>_meta</c> (<see cref="GatewayTokenVerifier"/>).
/// </summary>
public sealed class GatewayAuthenticationHandler : AuthenticationHandler<GatewayAuthenticationOptions>
{
    private readonly GatewaySettings _settings;

    public GatewayAuthenticationHandler(
        IOptionsMonitor<GatewayAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, GatewaySettings settings)
        : base(options, logger, encoder) => _settings = settings;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var gateway = _settings.Current;
        if (!gateway.IsEnabled) return Task.FromResult(AuthenticateResult.NoResult());
        if (!gateway.IsClientKey(gateway.PresentedKey(Request)))
        {
            Log.Warning("[MCP Gateway] Client key rejected");
            return Task.FromResult(AuthenticateResult.Fail("The gateway key is not valid"));
        }

        var identity = new ClaimsIdentity(GatewayPrincipals.ClientClaims(), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

/// <summary>The two principals of the gateway path: the gateway itself, and the person a verified token names.</summary>
public static class GatewayPrincipals
{
    public const string AuthenticationType = "MizanGatewayUser";

    /// <summary>
    /// The gateway, before a person is named. It holds the non-admin scopes, so prompts, skills and reference lists
    /// are open to it as to a connected app the person allowed everything but administration.
    /// </summary>
    public static IEnumerable<Claim> ClientClaims() =>
    [
        new(GatewayAccess.Claim, GatewayAccess.Path),
        new(GrantClaims.Scopes, string.Join(' ', McpScopes.All.Where(scope => scope != McpScopes.Admin && !scope.StartsWith("admin:", StringComparison.Ordinal)))),
    ];

    /// <summary>The person a verified token names. No role: the API reads theirs, and never serves an administrator's here.</summary>
    public static ClaimsPrincipal User(Guid userId) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim(GatewayAccess.Claim, GatewayAccess.Path),
        ],
        AuthenticationType));

    public static bool IsGateway(ClaimsPrincipal? principal) =>
        string.Equals(principal?.FindFirst(GatewayAccess.Claim)?.Value, GatewayAccess.Path, StringComparison.Ordinal);

    /// <summary>The gateway with no person named yet.</summary>
    public static bool IsClient(ClaimsPrincipal? principal) => IsGateway(principal) && principal!.FindFirst("sub") is null;
}
