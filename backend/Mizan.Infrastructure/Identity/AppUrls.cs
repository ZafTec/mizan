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
    /// Cookie domain shared by the web app and the API. Empty means host-only,
    /// which is what localhost wants; production sets ".mizan.zaftech.co".
    /// </summary>
    public string? CookieDomain { get; set; }
}

public partial class AppUrls : IAppUrls
{
    /// <summary>
    /// An allowlist, not a denylist: a return target is a same-origin path or
    /// it is discarded. Anything with a scheme, an authority or a backslash
    /// fails to match and falls back to the app root.
    /// </summary>
    [GeneratedRegex(@"^/[A-Za-z0-9\-._~!$&'()*+,;=:@/]*(\?[A-Za-z0-9\-._~!$&'()*+,;=:@/%?]*)?$")]
    private static partial Regex SafePath();

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
        if (candidate.StartsWith("//", StringComparison.Ordinal)) return _base.ToString();
        if (!SafePath().IsMatch(candidate)) return _base.ToString();

        return new Uri(_base, candidate.TrimStart('/')).ToString();
    }

    private string Build(string path, string token) =>
        new UriBuilder(new Uri(_base, path)) { Query = $"token={Uri.EscapeDataString(token)}" }.Uri.ToString();
}
