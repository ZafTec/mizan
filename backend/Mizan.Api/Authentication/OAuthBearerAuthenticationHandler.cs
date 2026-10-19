using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Mizan.Application.OAuth;

namespace Mizan.Api.Authentication;

public class OAuthBearerAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "OAuthBearer";
}

/// <summary>
/// How the native app signs in: the access token it got from the authorization server,
/// sent as a bearer token. It stands beside the browser's session cookie and reaches the
/// same endpoints as the same user. SignalR cannot set a header on a WebSocket, so on
/// hub paths the token may also arrive as the access_token query value.
/// </summary>
public class OAuthBearerAuthenticationHandler : AuthenticationHandler<OAuthBearerAuthenticationSchemeOptions>
{
    private readonly IMediator _mediator;

    public OAuthBearerAuthenticationHandler(
        IOptionsMonitor<OAuthBearerAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IMediator mediator)
        : base(options, logger, encoder) => _mediator = mediator;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken();
        if (token is null) return AuthenticateResult.NoResult();

        var who = await _mediator.Send(new ResolveApiAccessTokenQuery(token), Context.RequestAborted);
        if (who is null) return AuthenticateResult.Fail("The access token is not valid");

        var id = who.UserId.ToString();
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim("sub", id),
                new Claim(ClaimTypes.Role, who.Role),
                new Claim("role", who.Role),
                new Claim("type", "oauth_api"),
            },
            Scheme.Name,
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private string? ReadToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return header["Bearer ".Length..].Trim();

        // A query string is logged and cached more freely than a header, so it is allowed only where the
        // transport leaves no choice.
        if (Request.Path.StartsWithSegments("/hubs") && Request.Query.TryGetValue("access_token", out var query))
            return query.ToString();

        return null;
    }
}
