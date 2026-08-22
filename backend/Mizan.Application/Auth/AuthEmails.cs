using Mizan.Application.Interfaces;

namespace Mizan.Application.Auth;

/// <summary>
/// Pure. Builds the three transactional emails identity sends; the sender is
/// the only part that touches the network.
/// </summary>
public static class AuthEmails
{
    public static EmailMessage Verification(string to, string? name, string link) => Build(
        to,
        "Confirm your email",
        Greeting(name),
        "Confirm your address to finish setting up your Mizan account.",
        "Confirm email",
        link,
        "The link is good for 24 hours.");

    public static EmailMessage PasswordReset(string to, string? name, string link) => Build(
        to,
        "Reset your password",
        Greeting(name),
        "Use the link below to choose a new password. If you did not ask for this, ignore this email and nothing changes.",
        "Reset password",
        link,
        "The link is good for one hour and can only be used once.");

    public static EmailMessage SignInNotification(string to, string? name, string? ipAddress, string? userAgent)
    {
        var where = string.IsNullOrWhiteSpace(ipAddress) ? "an unknown address" : ipAddress;
        var what = string.IsNullOrWhiteSpace(userAgent) ? "an unknown device" : userAgent;
        var body = $"A new sign-in to your Mizan account from {where} using {what}. "
                 + "If that was not you, change your password and sign out every session from Settings.";

        var text = $"{Greeting(name)}\n\n{body}\n";
        var html = Wrap($"<p>{Escape(Greeting(name))}</p><p>{Escape(body)}</p>");
        return new EmailMessage(to, "New sign-in to your Mizan account", html, text);
    }

    private static EmailMessage Build(
        string to, string subject, string greeting, string body, string cta, string link, string footer)
    {
        var text = $"{greeting}\n\n{body}\n\n{link}\n\n{footer}\n";
        var html = Wrap(
            $"<p>{Escape(greeting)}</p>" +
            $"<p>{Escape(body)}</p>" +
            $"<p><a href=\"{Escape(link)}\" style=\"display:inline-block;padding:12px 20px;border-radius:12px;" +
            $"background:#0f766e;color:#ffffff;text-decoration:none;font-weight:600\">{Escape(cta)}</a></p>" +
            $"<p style=\"color:#64748b;font-size:13px\">{Escape(footer)}</p>" +
            $"<p style=\"color:#64748b;font-size:13px\">If the button does not work, paste this into your browser:<br>{Escape(link)}</p>");
        return new EmailMessage(to, subject, html, text);
    }

    private static string Greeting(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Hello," : $"Hello {name.Trim()},";

    private static string Wrap(string inner) =>
        "<div style=\"font-family:system-ui,-apple-system,Segoe UI,sans-serif;font-size:15px;line-height:1.6;color:#0f172a\">"
        + inner
        + "<p style=\"color:#94a3b8;font-size:12px\">Mizan</p></div>";

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
