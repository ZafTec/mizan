using Microsoft.Extensions.Options;
using Mizan.Infrastructure.Identity;

namespace Mizan.Api.Authentication;

/// <summary>
/// One place that knows how the session cookie is written and cleared, so the
/// login, OAuth callback and logout paths cannot drift apart.
/// </summary>
public class SessionCookie
{
    public const string Name = "mizan_session";

    private readonly AppOptions _options;
    private readonly IWebHostEnvironment _environment;

    public SessionCookie(IOptions<AppOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public void Write(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Build(expiresAt));

    public void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, Build(null));

    private CookieOptions Build(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        // Lax is enough: the web app and the API share a registrable domain, so
        // browser -> API is same-site and the cookie rides along.
        SameSite = SameSiteMode.Lax,
        Secure = !_environment.IsDevelopment(),
        Path = "/",
        Domain = string.IsNullOrWhiteSpace(_options.CookieDomain) ? null : _options.CookieDomain,
        Expires = expiresAt,
    };
}
