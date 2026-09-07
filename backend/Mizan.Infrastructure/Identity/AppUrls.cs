using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Identity;

public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Origin of the web app, e.g. https://mizan.zaftech.co.</summary>
    public string PublicUrl { get; set; } = "http://localhost:3000";

    /// <summary>
    /// Cookie domain override. Empty means host-only, which is correct in
    /// every environment now - the app, API (/api) and MCP server (/mcp) all
    /// live on the same host, so there is no other subdomain to share with.
    /// </summary>
    public string? CookieDomain { get; set; }
}

public partial class AppUrls : IAppUrls
{
    /// <summary>
    /// Return paths use a restricted URL alphabet. Canonical app URLs stored
    /// in OAuth state are reduced to a path before this validation.
    /// </summary>
    [GeneratedRegex(@"\A/[A-Za-z0-9\-._~!$&'()*+,;=:@/]*(\?[A-Za-z0-9\-._~!$&'()*+,;=:@/%?]*)?\z")]
    private static partial Regex SafePath();

    private readonly Uri _base;

    public AppUrls(IOptions<AppOptions> options)
    {
        if (!Uri.TryCreate(options.Value.PublicUrl, UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp)
            || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0
            || origin.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("App:PublicUrl must be the HTTP(S) origin of the web app.");
        }

        _base = origin;
    }

    public string VerifyEmail(string token) => Build("verifyemail", token);

    public string ResetPassword(string token) => Build("reset-password", token);

    public string SafeReturnUrl(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return _base.AbsoluteUri;

        // OAuth validates on both sides of the round trip. Only our exact
        // canonical origin (including the trailing slash) may be stripped.
        if (candidate.StartsWith(_base.AbsoluteUri, StringComparison.Ordinal))
        {
            candidate = candidate[(_base.AbsoluteUri.Length - 1)..];
        }

        if (candidate.StartsWith("//", StringComparison.Ordinal)) return _base.AbsoluteUri;
        if (!SafePath().IsMatch(candidate)) return _base.AbsoluteUri;

        // Keep the leading slash: stripping it would let /https://host/path
        // or /javascript:... become a URI with its own scheme.
        return new Uri(_base, candidate).AbsoluteUri;
    }

    private string Build(string path, string token) =>
        new UriBuilder(new Uri(_base, path)) { Query = $"token={Uri.EscapeDataString(token)}" }.Uri.ToString();
}
