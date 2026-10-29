using System.Net;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Mail;

/// <summary>
/// Pure. The emails an administrator causes: announcements, the contact form's
/// notice to administrators, and a reply to the person who wrote in. Each body
/// is plain text from a person, so it is escaped and kept as paragraphs.
/// </summary>
public static class MailTemplates
{
    public static OutgoingEmail Announcement(string to, string? name, string subject, string body) =>
        Build(to, subject, Greeting(name), body, "You receive this because you have a Mizan account.");

    /// <summary>A notification by email: the body, its facts one per line, then its links as absolute URLs.</summary>
    /// <summary>A notification as an email. A marketing message carries <paramref name="optOutUrl"/>, which turns news and offers off in one tap.</summary>
    public static OutgoingEmail Notification(
        string to,
        string? name,
        string title,
        string? body,
        IEnumerable<(string Label, string Value)> facts,
        IEnumerable<(string Label, string Url)> links,
        string? optOutUrl = null)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(body)) lines.Add(body.Trim());
        var factLines = facts.Select(f => $"{f.Label}: {f.Value}").ToList();
        if (factLines.Count > 0) lines.Add(string.Join('\n', factLines));
        var linkLines = links.Select(l => $"{l.Label}: {l.Url}").ToList();
        if (linkLines.Count > 0) lines.Add(string.Join('\n', linkLines));
        if (lines.Count == 0) lines.Add(title);
        return Build(to, title, Greeting(name), string.Join("\n\n", lines), NotificationFooter(optOutUrl));
    }

    /// <summary>The line under a notification email: where to choose, and for marketing the one-tap opt-out link.</summary>
    public static string NotificationFooter(string? optOutUrl) =>
        optOutUrl is { Length: > 0 }
            ? $"You get news and offers because you turned them on. Turn them off: {optOutUrl}"
            : "Choose which notifications reach you by email in Mizan, Settings, Notifications.";

    public static OutgoingEmail ContactReceived(
        string to, string fromName, string fromEmail, string subject, string message, string adminLink)
    {
        var body = $"{fromName} <{fromEmail}> wrote through the contact form.\n\nSubject: {subject}\n\n{message}\n\nReply from the admin area: {adminLink}";
        return Build(to, $"Contact: {subject}", "Hello,", body, "Replies go out from the admin area, so the sender sees the Mizan address.");
    }

    public static OutgoingEmail ContactReply(string to, string? name, string originalSubject, string reply) =>
        Build(
            to,
            originalSubject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? originalSubject : $"Re: {originalSubject}",
            Greeting(name),
            reply,
            "This is a reply to the message you sent through the Mizan contact form.");

    private static OutgoingEmail Build(string to, string subject, string greeting, string body, string footer)
    {
        var text = $"{greeting}\n\n{body.Trim()}\n\n{footer}\n";
        var paragraphs = body.Trim()
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => $"<p>{Escape(p).Replace("\n", "<br>")}</p>");
        var html =
            "<div style=\"font-family:system-ui,-apple-system,Segoe UI,sans-serif;font-size:15px;line-height:1.6;color:#0f172a\">"
            + $"<p>{Escape(greeting)}</p>"
            + string.Concat(paragraphs)
            + $"<p style=\"color:#64748b;font-size:13px\">{Escape(footer)}</p>"
            + "<p style=\"color:#94a3b8;font-size:12px\">Mizan</p></div>";
        return new OutgoingEmail(to, subject, html, text);
    }

    private static string Greeting(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Hello," : $"Hello {name.Trim()},";

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
