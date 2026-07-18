using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record NotificationDto(Guid Id, string Type, string Title, string? Body, string? LinkUrl, DateTime CreatedAt, DateTime? ReadAt);
public record NotificationListResult(IReadOnlyList<NotificationDto> Items, int UnreadCount, int TotalCount, int Page, int PageSize);

public record GetNotificationsQuery(int Page = 1, int PageSize = 20) : IRequest<NotificationListResult>;
public sealed class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, NotificationListResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public GetNotificationsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }

    public async Task<NotificationListResult> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = _context.Notifications.Where(n => n.UserId == userId);
        var total = await query.CountAsync(ct);
        var unread = await query.CountAsync(n => n.ReadAt == null, ct);
        var items = await query.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.LinkUrl, n.CreatedAt, n.ReadAt)).ToListAsync(ct);
        return new NotificationListResult(items, unread, total, page, pageSize);
    }
}

public record GetUnreadNotificationCountQuery : IRequest<int>;
public sealed class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public GetUnreadNotificationCountQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        return _context.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
    }
}
