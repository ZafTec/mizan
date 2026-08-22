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
        return NoContent();
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
    /// </summary>
    [HttpGet("external/{provider}")]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, [FromQuery] string? returnUrl)
    {
        var scheme = ExternalProviders.Resolve(provider)
            ?? throw new DomainValidationException($"Unknown sign-in provider '{provider}'.");

        var redirect = Url.Action(nameof(ExternalCallback), "Auth", new { returnUrl })
            ?? "/api/Auth/external/callback";

        return Challenge(new AuthenticationProperties { RedirectUri = redirect }, scheme);
    }

    [HttpGet("external/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalCallback([FromQuery] string? returnUrl)
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

        return Redirect(_urls.SafeReturnUrl(returnUrl));
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent() => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    public record LoginRequest(string Email, string Password);
    public record TokenRequest(string Token);
    public record EmailRequest(string Email);
    public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);
}
