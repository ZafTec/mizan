namespace Mizan.Application.Interfaces;

public interface INotificationWriter
{
    Task AddAsync(Guid userId, string type, string title, string? body = null, string? linkUrl = null, CancellationToken cancellationToken = default);
}
