using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Application.Telegram;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Outbox;

namespace Mizan.Infrastructure.Services;

/// <summary>
/// The one place a notification fans out: the row, a push job where a phone is
/// registered, and a Telegram message when the person has opted in
/// (docs/TELEGRAM.md#notifications-in-telegram). Every job is staged with the
/// caller's unit of work, so it goes out only if the notification is saved.
/// </summary>
public sealed class NotificationWriter : INotificationWriter
{
    private readonly IMizanDbContext _context;
    private readonly IOutbox _outbox;
    private readonly IPushSender _push;
    private readonly ITelegramMessenger _telegram;
    private readonly TimeProvider _time;

    public NotificationWriter(IMizanDbContext context, IOutbox outbox, IPushSender push, ITelegramMessenger telegram, TimeProvider time)
    {
        _context = context;
        _outbox = outbox;
        _push = push;
        _telegram = telegram;
        _time = time;
    }

    public async Task AddAsync(Guid userId, string type, string title, string? body = null, string? linkUrl = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            LinkUrl = linkUrl,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };
        _context.Notifications.Add(notification);

        // The push job is committed with the notification, so a notification that is saved is always
        // pushed and one that rolls back never is. Nothing is queued when there is nobody to send to.
        if (await _push.IsConfiguredAsync(cancellationToken) && await _context.DeviceTokens.AnyAsync(d => d.UserId == userId, cancellationToken))
        {
            await _outbox.EnqueueAsync(
                OutboxJobTypes.Push,
                new PushJob(userId, notification.Id, title, body, linkUrl),
                dedupeKey: $"push:{notification.Id}",
                cancellationToken: cancellationToken);
        }

        // Opt-in only. The messenger still checks that a chat is linked and the account is Pro.
        if (await _context.Users.AnyAsync(u => u.Id == userId && u.NotifyByTelegram, cancellationToken))
        {
            await _telegram.QueueAsync(
                userId,
                body is { Length: > 0 } ? $"{title}\n{body}" : title,
                MiniAppPaths.ForLink(linkUrl),
                dedupeKey: $"telegram:notification:{notification.Id}",
                cancellationToken);
        }
    }
}
