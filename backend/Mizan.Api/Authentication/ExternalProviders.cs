using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Mizan.Application.Interfaces;

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
