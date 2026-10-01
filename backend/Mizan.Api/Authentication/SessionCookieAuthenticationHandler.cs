using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;

namespace Mizan.Api.Authentication;

public class SessionCookieAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "SessionCookie";

    public string CookieName { get; set; } = SessionCookie.Name;
}

/// <summary>
/// The browser's only credential since v2: an opaque token in an httpOnly
/// cookie, resolved against user_sessions. Replaces the BetterAuth JWT bearer
/// scheme and everything that validated it - see docs/ARCHITECTURE.md#identity.
/// </summary>
public class SessionCookieAuthenticationHandler : AuthenticationHandler<SessionCookieAuthenticationSchemeOptions>
{
    private readonly ISessionService _sessions;
    private readonly IUserStatusService _userStatus;

    public SessionCookieAuthenticationHandler(
        IOptionsMonitor<SessionCookieAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISessionService sessions,
        IUserStatusService userStatus)
        : base(options, logger, encoder)
    {
        _sessions = sessions;
        _userStatus = userStatus;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(Options.CookieName, out var token) || string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        var session = await _sessions.ResolveIdentityAsync(token, Context.RequestAborted);
        if (session is null)
        {
            return AuthenticateResult.Fail("Session expired");
        }

        // Same gate the JWT path used, same cache: deleted, unverified and
        // banned users are turned away without a database round trip.
        var userId = session.UserId;
        var status = await _userStatus.GetStatusAsync(userId, Context.RequestAborted);
        if (!status.Exists) return AuthenticateResult.Fail("User not found");
        if (!status.EmailVerified) return AuthenticateResult.Fail("Email not verified");
        if (status.IsBanned) return AuthenticateResult.Fail("User banned");

        var id = userId.ToString();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id),
            new("sub", id),
            new(ClaimTypes.Role, status.Role),
            new("role", status.Role),
        };

        // An administrator viewing the site as this user. Who they are rides every request, so audit entries and the
        // restrictions below can name them.
        if (session.ImpersonatorId is { } impersonator)
        {
            claims.Add(new Claim(ImpersonationClaims.Impersonator, impersonator.ToString()));
            claims.Add(new Claim(ImpersonationClaims.Expires, session.ExpiresAt.ToString("O")));
        }

        var identity = new ClaimsIdentity(
            claims,
            Scheme.Name,
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
