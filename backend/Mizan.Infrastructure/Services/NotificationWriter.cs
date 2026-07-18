using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Services;

public sealed class NotificationWriter : INotificationWriter
{
    private readonly IMizanDbContext _context;

    public NotificationWriter(IMizanDbContext context) => _context = context;

    public Task AddAsync(Guid userId, string type, string title, string? body = null, string? linkUrl = null, CancellationToken cancellationToken = default)
    {
        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            LinkUrl = linkUrl,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
}
