using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Api.Authentication;
using Mizan.Application.Auth;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Identity;

namespace Mizan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISessionService _sessions;
    private readonly SessionCookie _cookie;
    private readonly IAppUrls _urls;

    public AuthController(IMediator mediator, ISessionService sessions, SessionCookie cookie, IAppUrls urls)
    {
        _mediator = mediator;
        _sessions = sessions;
        _cookie = cookie;
        _urls = urls;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthCredentials")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        await _mediator.Send(command);
        return Accepted(new { message = "Check your inbox to confirm your email address." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthCredentials")]
    public async Task<ActionResult<AuthUserDto>> Login([FromBody] LoginRequest request)
    {
        var result = await _mediator.Send(new LoginCommand(
            request.Email, request.Password, ClientIp(), UserAgent()));

        _cookie.Write(Response, result.SessionToken, DateTimeOffset.UtcNow.Add(SessionService.Lifetime));
        return Ok(result.User);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(SessionCookie.Name, out var token))
        {
            await _sessions.RevokeAsync(token);
        }

        _cookie.Clear(Response);
        _cookie.ClearAdmin(Response);
        return NoContent();
    }

    /// <summary>
    /// Ends an administrator's view of the site as a user and puts their own session back. If their own session has
    /// since expired they are signed out and sent to sign in again.
    /// </summary>
    [HttpPost("impersonation/stop")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> StopImpersonation()
    {
        Request.Cookies.TryGetValue(SessionCookie.Name, out var current);
        var impersonator = string.IsNullOrEmpty(current)
            ? null
            : await _mediator.Send(new StopImpersonationCommand(current));
        if (impersonator is null) throw new ForbiddenAccessException("This is not an administrator's view of the site.");

        // The administrator's own session comes back only if it is still good and still theirs.
        Request.Cookies.TryGetValue(SessionCookie.AdminName, out var adminToken);
        var admin = string.IsNullOrEmpty(adminToken) ? null : await _sessions.ResolveIdentityAsync(adminToken);
        _cookie.ClearAdmin(Response);

        if (admin is { ImpersonatorId: null } && admin.UserId == impersonator)
        {
            _cookie.Write(Response, adminToken!, DateTimeOffset.UtcNow.Add(SessionService.Lifetime));
            return Ok(new { restored = true });
        }

        _cookie.Clear(Response);
        return Ok(new { restored = false });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthUserDto>> Me()
    {
        var user = await _mediator.Send(new GetCurrentUserQuery());
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthCredentials")]
    public async Task<IActionResult> VerifyEmail([FromBody] TokenRequest request)
    {
        await _mediator.Send(new VerifyEmailCommand(request.Token));
        return NoContent();
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthEmail")]
    public async Task<IActionResult> ResendVerification([FromBody] EmailRequest request)
    {
        await _mediator.Send(new ResendVerificationCommand(request.Email));
        return Accepted(new { message = "If that address needs confirming, a new link is on its way." });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthEmail")]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailRequest request)
    {
        await _mediator.Send(new ForgotPasswordCommand(request.Email));
        return Accepted(new { message = "If that address has an account, a reset link is on its way." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthCredentials")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        Request.Cookies.TryGetValue(SessionCookie.Name, out var current);
        await _mediator.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword, current));
        return NoContent();
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<ActionResult<List<SessionSummaryDto>>> Sessions()
    {
        Request.Cookies.TryGetValue(SessionCookie.Name, out var current);
        return Ok(await _mediator.Send(new ListSessionsQuery(current)));
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        await _mediator.Send(new RevokeSessionCommand(sessionId));
        return NoContent();
    }

    [HttpDelete("account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount()
    {
        await _mediator.Send(new DeleteAccountCommand());
        _cookie.Clear(Response);
        return NoContent();
    }

    /// <summary>
    /// Starts an OAuth sign-in. The provider redirects back to
    /// <see cref="ExternalCallback"/>, which is where the session is minted.
    ///
    /// The return target is validated here and then carried inside the OAuth
    /// state, not on the callback's query string. It comes back out of a
    /// cookie this server wrote and signed, so nothing the caller controls
    /// reaches the redirect at the far end.
    /// </summary>
    [HttpGet("external/{provider}")]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, [FromQuery] string? returnUrl)
    {
        var scheme = ExternalProviders.Resolve(provider)
            ?? throw new DomainValidationException($"Unknown sign-in provider '{provider}'.");

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(ExternalCallback), "Auth") ?? ExternalProviders.CallbackPath,
        };
        properties.Items[ExternalProviders.ReturnUrlKey] = _urls.SafeReturnUrl(returnUrl);

        return Challenge(properties, scheme);
    }

    [HttpGet("external/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalCallback()
    {
        var result = await HttpContext.AuthenticateAsync(ExternalProviders.CookieScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return Redirect(_urls.SafeReturnUrl("/login?error=external_failed"));
        }

        var identity = ExternalProviders.Read(result);
        if (identity is null)
        {
            await HttpContext.SignOutAsync(ExternalProviders.CookieScheme);
            return Redirect(_urls.SafeReturnUrl("/login?error=external_no_email"));
        }

        var token = await _mediator.Send(new ExternalLoginCommand(
            identity.Provider, identity.ProviderKey, identity.Email, identity.Name, identity.Image,
            ClientIp(), UserAgent()));

        await HttpContext.SignOutAsync(ExternalProviders.CookieScheme);
        _cookie.Write(Response, token, DateTimeOffset.UtcNow.Add(SessionService.Lifetime));

        return Redirect(ExternalProviders.ReturnUrl(result, _urls));
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent() => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    public record LoginRequest(string Email, string Password);
    public record TokenRequest(string Token);
    public record EmailRequest(string Email);
    public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);
}
