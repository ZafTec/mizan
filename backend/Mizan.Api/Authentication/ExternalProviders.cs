using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Identity;

namespace Mizan.Api.Authentication;

public record ExternalIdentity(string Provider, string ProviderKey, string Email, string? Name, string? Image);

/// <summary>
/// The OAuth handlers sign into a short-lived cookie of their own; the callback
/// reads it once, mints a real session and signs it back out. That cookie is
/// the only reason a second scheme exists.
/// </summary>
public static class ExternalProviders
{
    public const string CookieScheme = "External";
    public const string CallbackPath = "/api/Auth/external/callback";

    /// <summary>Key under which the validated return path rides in the OAuth state.</summary>
    public const string ReturnUrlKey = "mizan.returnUrl";

    public static string? Resolve(string provider) => provider?.ToLowerInvariant() switch
    {
        "google" => GoogleDefaults.AuthenticationScheme,
        "github" => GitHubAuthenticationDefaults.AuthenticationScheme,
        _ => null,
    };

    /// <summary>
    /// Keep the provider registrations made by Better Auth, with callbacks
    /// returning to the canonical public web origin.
    /// </summary>
    public static void Configure(OAuthOptions options, IConfiguration configuration, string provider)
    {
        provider = provider.ToLowerInvariant();
        var authorizationEndpoint = provider switch
        {
            "google" => GoogleDefaults.AuthorizationEndpoint,
            "github" => GitHubAuthenticationDefaults.AuthorizationEndpoint,
            _ => throw new ArgumentException("Unsupported sign-in provider.", nameof(provider)),
        };
        var allowedAuthorizationUri = new Uri(authorizationEndpoint);

        var app = configuration.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions();
        if (!Uri.TryCreate(app.PublicUrl, UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp)
            || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0
            || origin.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("App:PublicUrl must be the HTTP(S) origin of the web app.");
        }

        var callbackPath = configuration[$"Authentication:{provider}:CallbackPath"]
            ?? $"/api/auth/callback/{provider}";
        if (!callbackPath.StartsWith('/') || callbackPath.StartsWith("//", StringComparison.Ordinal)
            || callbackPath.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_')))
        {
            throw new InvalidOperationException($"Authentication:{provider}:CallbackPath must be an absolute URL path.");
        }

        options.SignInScheme = CookieScheme;
        options.CallbackPath = callbackPath;
        options.CorrelationCookie.Domain = string.IsNullOrWhiteSpace(app.CookieDomain) ? null : app.CookieDomain;
        options.CorrelationCookie.Path = "/";
        var callbackUri = new Uri(origin, callbackPath).AbsoluteUri;
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            if (!Uri.TryCreate(context.RedirectUri, UriKind.Absolute, out var requestedAuthorizationUri)
                || !string.Equals(requestedAuthorizationUri.GetLeftPart(UriPartial.Path), authorizationEndpoint, StringComparison.Ordinal)
                || requestedAuthorizationUri.UserInfo.Length != 0 || requestedAuthorizationUri.Fragment.Length != 0)
            {
                throw new InvalidOperationException("Unexpected OAuth authorization destination.");
            }

            // Use the registered public origin regardless of the request host.
            // The callback proxy must preserve that host and trusted HTTPS
            // scheme for token exchange.
            var query = QueryHelpers.ParseQuery(requestedAuthorizationUri.Query);
            query["redirect_uri"] = callbackUri;
            var authorizationUri = new UriBuilder(authorizationEndpoint)
            {
                Query = QueryString.Create(query).ToString(),
            }.Uri;

            // Build from the provider constant and enforce its exact endpoint
            // at the redirect boundary. Query values cannot choose a host/path.
            if (authorizationUri.Scheme == Uri.UriSchemeHttps
                && authorizationUri.Host == allowedAuthorizationUri.Host
                && authorizationUri.Port == allowedAuthorizationUri.Port
                && authorizationUri.AbsolutePath == allowedAuthorizationUri.AbsolutePath
                && authorizationUri.UserInfo.Length == 0 && authorizationUri.Fragment.Length == 0)
            {
                context.Response.Redirect(authorizationUri.AbsoluteUri);
                return Task.CompletedTask;
            }

            throw new InvalidOperationException("Unexpected OAuth authorization destination.");
        };
    }

    /// <summary>
    /// The return target we put into the state before the round trip. It is
    /// re-validated on the way out: the encrypted cookie should be
    /// untamperable, but a redirect is not the place to rely on "should".
    /// </summary>
    public static string ReturnUrl(AuthenticateResult result, IAppUrls urls)
    {
        string? stored = null;
        result.Properties?.Items.TryGetValue(ReturnUrlKey, out stored);
        return urls.SafeReturnUrl(stored);
    }

    public static ExternalIdentity? Read(AuthenticateResult result)
    {
        var principal = result.Principal;
        if (principal is null) return null;

        var provider = result.Properties?.Items[".AuthScheme"] ?? result.Ticket?.AuthenticationScheme;
        var key = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrWhiteSpace(provider)
            || string.IsNullOrWhiteSpace(key)
            || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new ExternalIdentity(
            provider.ToLowerInvariant(),
            key,
            email,
            principal.FindFirst(ClaimTypes.Name)?.Value,
            principal.FindFirst("urn:github:avatar")?.Value
                ?? principal.FindFirst("picture")?.Value
                ?? principal.FindFirst("urn:google:picture")?.Value);
    }
}
