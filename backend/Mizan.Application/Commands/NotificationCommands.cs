using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record MarkNotificationReadCommand(Guid Id) : IRequest;
public sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public MarkNotificationReadCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var row = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == request.Id && n.UserId == userId, ct)
            ?? throw new EntityNotFoundException("Notification not found");
        row.ReadAt ??= DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }
}

public record MarkAllNotificationsReadCommand : IRequest;
public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public MarkAllNotificationsReadCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        await _context.Notifications.Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
    }
}
