using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Outbox;

namespace Mizan.Infrastructure.Services;

public sealed class NotificationWriter : INotificationWriter
{
    private readonly IMizanDbContext _context;
    private readonly IOutbox _outbox;
    private readonly IPushSender _push;
    private readonly TimeProvider _time;

    public NotificationWriter(IMizanDbContext context, IOutbox outbox, IPushSender push, TimeProvider time)
    {
        _context = context;
        _outbox = outbox;
        _push = push;
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
        if (_push.IsConfigured && await _context.DeviceTokens.AnyAsync(d => d.UserId == userId, cancellationToken))
        {
            await _outbox.EnqueueAsync(
                OutboxJobTypes.Push,
                new PushJob(userId, notification.Id, title, body, linkUrl),
                dedupeKey: $"push:{notification.Id}",
                cancellationToken: cancellationToken);
        }
    }
}
