using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

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

    public static string? Resolve(string provider) => provider?.ToLowerInvariant() switch
    {
        "google" => GoogleDefaults.AuthenticationScheme,
        "github" => GitHubAuthenticationDefaults.AuthenticationScheme,
        _ => null,
    };

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
