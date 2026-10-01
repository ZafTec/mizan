namespace Mizan.Application.OAuth;

/// <summary>
/// Which redirect URIs a client may register and how a request is matched to
/// them. Matching is exact, except that a loopback address may use any port
/// (RFC 8252 section 7.3), because a desktop client picks a free port at run time.
/// </summary>
public static class RedirectUriRules
{
    private static readonly HashSet<string> ForbiddenSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "javascript", "data", "file", "vbscript", "about", "blob", "ftp", "ws", "wss", "chrome", "view-source",
    };

    public static bool TryValidate(string? value, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512)
        {
            reason = "Redirect URI is missing or too long.";
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            reason = "Redirect URI must be an absolute URI.";
            return false;
        }

        if (uri.Fragment.Length != 0 || value.Contains('#'))
        {
            reason = "Redirect URI must not contain a fragment.";
            return false;
        }

        if (uri.UserInfo.Length != 0)
        {
            reason = "Redirect URI must not contain credentials.";
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps) return true;

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            if (IsLoopback(uri)) return true;
            reason = "Plain http redirect URIs are allowed only for localhost.";
            return false;
        }

        if (ForbiddenSchemes.Contains(uri.Scheme) || uri.Scheme == Uri.UriSchemeHttp)
        {
            reason = $"Scheme '{uri.Scheme}' is not allowed.";
            return false;
        }

        // A private-use scheme for a native app, such as com.example.app:/callback.
        return true;
    }

    public static bool IsLoopback(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp
        && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host == "127.0.0.1"
            || uri.Host == "[::1]"
            || uri.Host == "::1");

    public static bool Matches(string registered, string requested)
    {
        if (string.Equals(registered, requested, StringComparison.Ordinal)) return true;
        if (!Uri.TryCreate(registered, UriKind.Absolute, out var a)
            || !Uri.TryCreate(requested, UriKind.Absolute, out var b)) return false;
        if (!IsLoopback(a) || !IsLoopback(b)) return false;

        return string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.PathAndQuery, b.PathAndQuery, StringComparison.Ordinal);
    }
}
