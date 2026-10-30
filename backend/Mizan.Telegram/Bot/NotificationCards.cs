using Mizan.Contracts.Telegram;

namespace Mizan.Telegram.Bot;

/// <summary>A notification ready to send: the HTML, the picture to send it as a caption of (or null), and the buttons.</summary>
public sealed record RenderedNotification(string Html, string? PhotoUrl, InlineKeyboardMarkup? Keyboard);

/// <summary>
/// How a notification looks in Telegram (docs/TELEGRAM.md#notifications-in-telegram): the title in
/// bold, the body, then one line per fact. With a picture it is the picture's caption, which Telegram
/// caps at <see cref="CaptionLimit"/> characters, otherwise a message, capped at <see cref="TextLimit"/>;
/// a body too long for the cap is shortened, never the title or facts. Each button opens its Mini App
/// page when it has one and the Mini App is served over https, else its web page when that is https;
/// a button that can do neither is left out, because Telegram refuses the whole message over it.
/// A reminder's card adds a last row of buttons the bot answers itself (Done, Snooze, Skip today).
/// A request without a card (queued before cards existed) is its text with one <b>Open</b> button.
/// </summary>
public static class NotificationCards
{
    public const int CaptionLimit = 1024;
    public const int TextLimit = 4096;

    public static RenderedNotification Render(TelegramNotifyRequest request, TelegramBotOptions options, bool asPhoto = true)
    {
        if (request.Card is not { } card)
        {
            var keyboard = request.OpenPath is { Length: > 0 } path && options.MiniAppButtonUrl(path) is { } url
                ? InlineKeyboardMarkup.Column(InlineKeyboardButton.OpenWebApp("Open", url))
                : null;
            return new RenderedNotification(Fit(Escape(request.Text), TextLimit), null, keyboard);
        }

        var photo = asPhoto && IsHttps(card.ImageUrl) ? card.ImageUrl : null;
        var limit = photo is null ? TextLimit : CaptionLimit;

        var head = $"<b>{Escape(card.Title)}</b>";
        var facts = card.Facts is { Count: > 0 }
            ? string.Join('\n', card.Facts.Select(f => $"{Escape(f.Label)}: <b>{Escape(f.Value)}</b>"))
            : null;
        var fixedLength = head.Length + (facts is null ? 0 : facts.Length + 2);
        var body = card.Body is { Length: > 0 } text ? Shorten(text, limit - fixedLength - 1) : null;

        var html = head;
        if (body is { Length: > 0 }) html += "\n" + body;
        if (facts is not null) html += "\n\n" + facts;

        var buttons = (card.Buttons ?? [])
            .Select(b => Button(b, options))
            .OfType<InlineKeyboardButton>()
            .ToArray();
        var rows = buttons.Select(b => (IReadOnlyList<InlineKeyboardButton>)[b]).ToList();
        // A reminder's Done, Snooze and Skip today answer here in the chat, side by side under the links.
        if (card.Callbacks is { Count: > 0 } callbacks)
            rows.Add(callbacks.Select(c => InlineKeyboardButton.Callback(c.Label, c.Data)).ToList());
        return new RenderedNotification(Fit(html, limit), photo, rows.Count == 0 ? null : new InlineKeyboardMarkup { InlineKeyboard = rows });
    }

    private static InlineKeyboardButton? Button(TelegramNotifyButton button, TelegramBotOptions options)
    {
        if (button.MiniAppPath is { } page && options.MiniAppButtonUrl(page) is { } miniApp)
            return InlineKeyboardButton.OpenWebApp(button.Label, miniApp);

        if (button.WebPath is { Length: > 0 } path)
        {
            var url = Uri.TryCreate(path, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
                ? path
                : $"{options.PublicUrl.TrimEnd('/')}/{path.TrimStart('/')}";
            if (IsHttps(url)) return InlineKeyboardButton.OpenUrl(button.Label, url);
        }
        return null;
    }

    /// <summary>The body escaped, cut at a word so that escaped it fits <paramref name="room"/> characters, with an ellipsis.</summary>
    private static string? Shorten(string text, int room)
    {
        if (room <= 1) return null;
        var escaped = Escape(text);
        if (escaped.Length <= room) return escaped;

        var cut = text;
        while (cut.Length > 0 && Escape(cut).Length + 1 > room)
        {
            var space = cut.LastIndexOf(' ', Math.Max(0, cut.Length - 2));
            cut = space > 0 ? cut[..space] : cut[..^1];
        }
        return cut.Length == 0 ? null : Escape(cut.TrimEnd()) + "…";
    }

    /// <summary>A last guard for a title or facts longer than the cap on their own: cut, never mid-tag or mid-entity.</summary>
    private static string Fit(string html, int limit)
    {
        if (html.Length <= limit) return html;
        var cut = html[..(limit - 5)];
        var tag = cut.LastIndexOf('<');
        if (tag > cut.LastIndexOf('>')) cut = cut[..tag];
        var entity = cut.LastIndexOf('&');
        if (entity > cut.LastIndexOf(';')) cut = cut[..entity];
        // A <b> left open would make Telegram refuse the HTML.
        if (cut.Split("<b>").Length > cut.Split("</b>").Length) cut += "</b>";
        return cut + "…";
    }

    private static bool IsHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
