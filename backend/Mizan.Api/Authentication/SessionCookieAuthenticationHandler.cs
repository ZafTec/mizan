using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;

namespace Mizan.Api.Authentication;

public class SessionCookieAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "SessionCookie";

    public string CookieName { get; set; } = SessionCookie.Name;
}

/// <summary>
/// The browser's only credential since v2: an opaque token in an httpOnly
/// cookie, resolved against user_sessions. Replaces the BetterAuth JWT bearer
/// scheme and everything that validated it - see docs/REFOCUS.md §6.
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

        var userId = await _sessions.ResolveAsync(token, Context.RequestAborted);
        if (userId is null)
        {
            return AuthenticateResult.Fail("Session expired");
        }

        // Same gate the JWT path used, same cache: deleted, unverified and
        // banned users are turned away without a database round trip.
        var status = await _userStatus.GetStatusAsync(userId.Value, Context.RequestAborted);
        if (!status.Exists) return AuthenticateResult.Fail("User not found");
        if (!status.EmailVerified) return AuthenticateResult.Fail("Email not verified");
        if (status.IsBanned) return AuthenticateResult.Fail("User banned");

        var id = userId.Value.ToString();
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim("sub", id),
                new Claim(ClaimTypes.Role, status.Role),
                new Claim("role", status.Role),
            },
            Scheme.Name,
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
