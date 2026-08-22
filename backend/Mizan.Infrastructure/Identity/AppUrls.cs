using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Identity;

public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Origin of the web app, e.g. https://mizan.euaell.me.</summary>
    public string PublicUrl { get; set; } = "http://localhost:3000";

    /// <summary>
    /// Cookie domain shared by the web app and the API. Empty means host-only,
    /// which is what localhost wants; production sets ".euaell.me".
    /// </summary>
    public string? CookieDomain { get; set; }
}

public class AppUrls : IAppUrls
{
    private readonly Uri _base;

    public AppUrls(IOptions<AppOptions> options)
    {
        _base = new Uri(options.Value.PublicUrl.TrimEnd('/') + "/");
    }

    public string VerifyEmail(string token) => Build("verifyemail", token);

    public string ResetPassword(string token) => Build("reset-password", token);

    public string SafeReturnUrl(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return _base.ToString();

        // Only relative paths are honoured. An absolute URL in a query string
        // is an open redirect waiting to happen, and we never need one.
        if (!Uri.TryCreate(candidate, UriKind.Relative, out _)) return _base.ToString();
        if (candidate.StartsWith("//", StringComparison.Ordinal)) return _base.ToString();
        if (!candidate.StartsWith('/')) return _base.ToString();

        return new Uri(_base, candidate.TrimStart('/')).ToString();
    }

    private string Build(string path, string token) =>
        new UriBuilder(new Uri(_base, path)) { Query = $"token={Uri.EscapeDataString(token)}" }.Uri.ToString();
}
