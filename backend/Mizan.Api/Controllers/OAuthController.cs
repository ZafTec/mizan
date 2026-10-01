using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.Interfaces;
using Mizan.Application.OAuth;
using Mizan.Contracts.Mcp;

namespace Mizan.Api.Controllers;

/// <summary>
/// The OAuth 2.1 authorization server for MCP clients and our own apps. Public
/// endpoints (token, register, revoke, discovery) are called by software, so they
/// allow any origin without credentials. The consent endpoints are called by our
/// web app with the session cookie.
/// </summary>
[ApiController]
[Route("api/oauth")]
public class OAuthController : ControllerBase
{
    public const string PublicCorsPolicy = "OAuthPublic";

    private readonly IMediator _mediator;
    private readonly IOAuthSettings _settings;

    public OAuthController(IMediator mediator, IOAuthSettings settings)
    {
        _mediator = mediator;
        _settings = settings;
    }

    // Both paths exist because clients try the OAuth name first and the OpenID name as a fallback.
    [HttpGet("/api/.well-known/openid-configuration")]
    [HttpGet("/api/.well-known/oauth-authorization-server")]
    [AllowAnonymous]
    [EnableCors(PublicCorsPolicy)]
    public IActionResult Discovery()
    {
        var issuer = _settings.Issuer;
        return Ok(new Dictionary<string, object>
        {
            ["issuer"] = issuer,
            ["authorization_endpoint"] = $"{issuer}/oauth/authorize",
            ["token_endpoint"] = $"{issuer}/oauth/token",
            ["registration_endpoint"] = $"{issuer}/oauth/register",
            ["revocation_endpoint"] = $"{issuer}/oauth/revoke",
            ["response_types_supported"] = new[] { "code" },
            ["response_modes_supported"] = new[] { "query" },
            ["grant_types_supported"] = new[] { "authorization_code", "refresh_token" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
            ["token_endpoint_auth_methods_supported"] = new[] { "none" },
            ["scopes_supported"] = McpScopes.All.Where(s => s != McpScopes.Admin).ToArray(),
            ["client_id_metadata_document_supported"] = _settings.AllowMetadataClients,
            ["authorization_response_iss_parameter_supported"] = true,
            ["subject_types_supported"] = new[] { "public" },
        });
    }

    [HttpGet("authorize")]
    [AllowAnonymous]
    [EnableRateLimiting("OAuth")]
    public async Task<IActionResult> Authorize(
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "scope")] string? scope,
        [FromQuery(Name = "state")] string? state,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromQuery(Name = "resource")] string? resource)
    {
        var result = await _mediator.Send(new StartAuthorizationCommand(
            clientId, redirectUri, responseType, scope, state, codeChallenge, codeChallengeMethod, resource));

        if (result.RequestSecret is not null)
        {
            var consent = _settings.ConsentUrl;
            var separator = consent.Contains('?') ? "&" : "?";
            return Redirect($"{consent}{separator}request={Uri.EscapeDataString(result.RequestSecret)}");
        }

        // Only a client and redirect URI we have already matched may receive the error.
        if (result.RedirectBackTo is { } back)
        {
            var parts = new List<string>
            {
                $"error={Uri.EscapeDataString(result.Error ?? "invalid_request")}",
                $"error_description={Uri.EscapeDataString(result.ErrorDescription ?? string.Empty)}",
            };
            if (result.State is not null) parts.Add($"state={Uri.EscapeDataString(result.State)}");
            parts.Add($"iss={Uri.EscapeDataString(_settings.Issuer)}");
            return Redirect(back + (back.Contains('?') ? "&" : "?") + string.Join("&", parts));
        }

        return BadRequest(new { error = result.Error, error_description = result.ErrorDescription });
    }

    [HttpPost("token")]
    [AllowAnonymous]
    [EnableCors(PublicCorsPolicy)]
    [EnableRateLimiting("OAuth")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token([FromForm] TokenForm form)
    {
        NoStore();
        try
        {
            var result = await _mediator.Send(new ExchangeOAuthTokenCommand(
                form.GrantType, form.ClientId, form.Code, form.CodeVerifier, form.RedirectUri, form.RefreshToken));
            return Ok(new
            {
                access_token = result.AccessToken,
                token_type = result.TokenType,
                expires_in = result.ExpiresIn,
                refresh_token = result.RefreshToken,
                scope = result.Scope,
            });
        }
        catch (OAuthException ex)
        {
            return OAuthError(ex);
        }
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableCors(PublicCorsPolicy)]
    [EnableRateLimiting("OAuthRegister")]
    public async Task<IActionResult> Register([FromBody] RegisterBody body)
    {
        NoStore();
        try
        {
            var result = await _mediator.Send(new RegisterOAuthClientCommand(
                body.ClientName, body.RedirectUris, body.LogoUri, body.ClientUri,
                body.TokenEndpointAuthMethod, body.GrantTypes, body.ResponseTypes));
            return StatusCode(StatusCodes.Status201Created, new
            {
                client_id = result.ClientId,
                client_id_issued_at = result.ClientIdIssuedAt,
                client_name = result.ClientName,
                redirect_uris = result.RedirectUris,
                token_endpoint_auth_method = "none",
                grant_types = new[] { "authorization_code", "refresh_token" },
                response_types = new[] { "code" },
            });
        }
        catch (OAuthException ex)
        {
            return OAuthError(ex);
        }
    }

    [HttpPost("revoke")]
    [AllowAnonymous]
    [EnableCors(PublicCorsPolicy)]
    [EnableRateLimiting("OAuth")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Revoke([FromForm] RevokeForm form)
    {
        NoStore();
        await _mediator.Send(new RevokeOAuthTokenCommand(form.Token, form.ClientId));
        return Ok();
    }

    // The consent screen. These use POST so the request secret stays out of URLs and logs.

    [HttpPost("authorization-requests/view")]
    [Authorize]
    public async Task<ActionResult<AuthorizationRequestView>> ViewRequest([FromBody] RequestSecretBody body) =>
        Ok(await _mediator.Send(new GetAuthorizationRequestQuery(body.Request)));

    [HttpPost("authorization-requests/decision")]
    [Authorize]
    public async Task<ActionResult<DecideAuthorizationResult>> Decide([FromBody] DecisionBody body) =>
        Ok(await _mediator.Send(new DecideAuthorizationCommand(
            body.Request, body.Approve, body.Scopes, body.HouseholdMode, body.HouseholdIds)));

    /// <summary>Called by the MCP server with its service key for every access token it has not seen recently.</summary>
    [HttpPost("introspect")]
    [Authorize(Policy = "McpService")]
    public async Task<ActionResult<OAuthIntrospection>> Introspect([FromBody] IntrospectBody body) =>
        Ok(await _mediator.Send(new IntrospectOAuthTokenQuery(body.Token ?? string.Empty, body.Audience ?? "mcp")));

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private IActionResult OAuthError(OAuthException ex) =>
        StatusCode(ex.Status, new { error = ex.Error, error_description = ex.Message });

    public sealed class TokenForm
    {
        [FromForm(Name = "grant_type")] public string? GrantType { get; set; }
        [FromForm(Name = "client_id")] public string? ClientId { get; set; }
        [FromForm(Name = "code")] public string? Code { get; set; }
        [FromForm(Name = "code_verifier")] public string? CodeVerifier { get; set; }
        [FromForm(Name = "redirect_uri")] public string? RedirectUri { get; set; }
        [FromForm(Name = "refresh_token")] public string? RefreshToken { get; set; }
    }

    public sealed class RevokeForm
    {
        [FromForm(Name = "token")] public string? Token { get; set; }
        [FromForm(Name = "client_id")] public string? ClientId { get; set; }
    }

    public sealed class RegisterBody
    {
        [JsonPropertyName("client_name")] public string? ClientName { get; set; }
        [JsonPropertyName("redirect_uris")] public List<string>? RedirectUris { get; set; }
        [JsonPropertyName("logo_uri")] public string? LogoUri { get; set; }
        [JsonPropertyName("client_uri")] public string? ClientUri { get; set; }
        [JsonPropertyName("token_endpoint_auth_method")] public string? TokenEndpointAuthMethod { get; set; }
        [JsonPropertyName("grant_types")] public List<string>? GrantTypes { get; set; }
        [JsonPropertyName("response_types")] public List<string>? ResponseTypes { get; set; }
    }

    public sealed record RequestSecretBody(string Request);

    public sealed record DecisionBody(
        string Request, bool Approve, List<string>? Scopes, string? HouseholdMode, List<Guid>? HouseholdIds);

    public sealed record IntrospectBody(string? Token, string? Audience);
}
