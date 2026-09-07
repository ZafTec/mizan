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
    /// Keep the provider registrations made by Better Auth. The API can live
    /// on a separate subdomain, while providers return to the public web host.
    /// </summary>
    public static void Configure(OAuthOptions options, IConfiguration configuration, string provider)
    {
        provider = provider.ToLowerInvariant();
        if (Resolve(provider) is null) throw new ArgumentException("Unsupported sign-in provider.", nameof(provider));

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
            // The challenge may arrive on api.example.com, but the registered
            // redirect_uri belongs to the web origin. The callback proxy must
            // preserve that host and trusted HTTPS scheme for token exchange.
            var authorizationUri = new UriBuilder(context.RedirectUri);
            var query = QueryHelpers.ParseQuery(authorizationUri.Query);
            query["redirect_uri"] = callbackUri;
            authorizationUri.Query = QueryString.Create(query).ToString();
            context.Response.Redirect(authorizationUri.Uri.AbsoluteUri);
            return Task.CompletedTask;
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
