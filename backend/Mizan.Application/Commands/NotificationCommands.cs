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

/// <summary>Marks several of the caller's notifications read; ids that are not theirs are passed over. Answers how many were unread and now are not.</summary>
public record MarkNotificationsReadCommand(IReadOnlyList<Guid> Ids) : IRequest<int>;
public sealed class MarkNotificationsReadCommandHandler : IRequestHandler<MarkNotificationsReadCommand, int>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public MarkNotificationsReadCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<int> Handle(MarkNotificationsReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var ids = (request.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0) throw new DomainValidationException("Give the ids of the notifications to mark read, or \"all\".");
        if (ids.Count > 200) throw new DomainValidationException("Mark at most 200 notifications at once, or pass \"all\".");
        return await _context.Notifications.Where(n => n.UserId == userId && n.ReadAt == null && ids.Contains(n.Id))
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
    }
}
