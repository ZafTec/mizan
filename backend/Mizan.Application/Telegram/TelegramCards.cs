using Mizan.Application.Interfaces;
using Mizan.Contracts.Notifications;
using Mizan.Contracts.Telegram;

namespace Mizan.Application.Telegram;

/// <summary>
/// A notification as a Telegram message (docs/TELEGRAM.md#notifications-in-telegram): the plain text
/// (title, then body) every message keeps, the Mini App page of its link, and the card the bot
/// renders. Each action becomes a button; a notice without actions gets one <b>Open</b> button for
/// its link. A button's Mini App page comes from <see cref="MiniAppPaths"/>; a link the Mini App has
/// no page for keeps its web path, which the bot opens in the browser. A reminder also gets
/// <b>Done</b>, <b>Snooze 30 min</b> and <b>Skip today</b>, which the bot answers itself.
/// </summary>
public static class TelegramCards
{
    public static TelegramOutgoing For(NotificationMessage message, string? linkUrl, Guid? reminderId = null)
    {
        var text = message.Body is { Length: > 0 } body ? $"{message.Title}\n{body}" : message.Title;
        var openPath = MiniAppPaths.ForLink(linkUrl);

        IReadOnlyList<TelegramNotifyButton> buttons = message.Actions is { Count: > 0 } actions
            ? actions.Take(NotificationMessage.MaxActions)
                .Select(a => new TelegramNotifyButton(a.Label, MiniAppPaths.ForLink(a.Path), a.Path))
                .ToList()
            : linkUrl is { Length: > 0 } link
                ? [new TelegramNotifyButton("Open", openPath, link)]
                : [];

        return new TelegramOutgoing(
            text,
            openPath,
            new TelegramNotifyCard(
                message.Title,
                message.Body,
                message.ImageUrl,
                buttons,
                message.Facts ?? [],
                reminderId is { } id ? TelegramReminderCallbacks.For(id) : null));
    }
}
