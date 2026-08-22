using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Admin;

public record AdminUserDto(
    Guid Id,
    string Email,
    string? Name,
    string? Image,
    string Role,
    bool EmailVerified,
    bool Banned,
    string? BanReason,
    DateTime? BanExpires,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record AdminSessionDto(
    Guid Id,
    Guid UserId,
    string? UserName,
    string? UserEmail,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt,
    DateTime LastSeenAt,
    DateTime ExpiresAt);

public record AdminUserDetailDto(
    AdminUserDto User,
    int ActiveSessionCount,
    List<AdminSessionDto> RecentSessions);

public record AdminOverviewDto(
    int TotalUsers,
    int ActiveTrainers,
    int BannedUsers,
    int ActiveSessions,
    List<AdminUserDto> RecentUsers);

public record GetAdminOverviewQuery : IRequest<AdminOverviewDto>;

public class GetAdminOverviewQueryHandler : IRequestHandler<GetAdminOverviewQuery, AdminOverviewDto>
{
    private readonly IMizanDbContext _context;

    public GetAdminOverviewQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<AdminOverviewDto> Handle(GetAdminOverviewQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return new AdminOverviewDto(
            await _context.Users.CountAsync(cancellationToken),
            await _context.Users.CountAsync(u => u.Role == "trainer", cancellationToken),
            await _context.Users.CountAsync(u => u.Banned, cancellationToken),
            await _context.UserSessions.CountAsync(s => s.ExpiresAt > now, cancellationToken),
            await _context.Users.AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .Select(AdminUserProjection.Expression)
                .ToListAsync(cancellationToken));
    }
}

public record ListAdminUsersQuery : IRequest<PagedResult<AdminUserDto>>, IPagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Role { get; init; }
    public bool? Banned { get; init; }
}

public class ListAdminUsersQueryHandler : IRequestHandler<ListAdminUsersQuery, PagedResult<AdminUserDto>>
{
    private readonly IMizanDbContext _context;

    public ListAdminUsersQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<PagedResult<AdminUserDto>> Handle(ListAdminUsersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.ToLower().Contains(term)
                || (u.Name != null && u.Name.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.Where(u => u.Role == request.Role);
        }

        if (request.Banned is { } banned)
        {
            query = query.Where(u => u.Banned == banned);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AdminUserProjection.Expression)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminUserDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }
}

public record GetAdminUserQuery(Guid UserId) : IRequest<AdminUserDetailDto>;

public class GetAdminUserQueryHandler : IRequestHandler<GetAdminUserQuery, AdminUserDetailDto>
{
    private readonly IMizanDbContext _context;

    public GetAdminUserQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<AdminUserDetailDto> Handle(GetAdminUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(AdminUserProjection.Expression)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        var now = DateTime.UtcNow;
        var sessions = await _context.UserSessions.AsNoTracking()
            .Where(s => s.UserId == request.UserId)
            .OrderByDescending(s => s.LastSeenAt)
            .Take(5)
            .Select(s => new AdminSessionDto(
                s.Id, s.UserId, user.Name, user.Email,
                s.IpAddress, s.UserAgent, s.CreatedAt, s.LastSeenAt, s.ExpiresAt))
            .ToListAsync(cancellationToken);

        var active = await _context.UserSessions
            .CountAsync(s => s.UserId == request.UserId && s.ExpiresAt > now, cancellationToken);

        return new AdminUserDetailDto(user, active, sessions);
    }
}

public record ListAdminSessionsQuery : IRequest<PagedResult<AdminSessionDto>>, IPagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public bool ActiveOnly { get; init; } = true;
}

public class ListAdminSessionsQueryHandler : IRequestHandler<ListAdminSessionsQuery, PagedResult<AdminSessionDto>>
{
    private readonly IMizanDbContext _context;

    public ListAdminSessionsQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<PagedResult<AdminSessionDto>> Handle(ListAdminSessionsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var now = DateTime.UtcNow;

        var query = _context.UserSessions.AsNoTracking();
        if (request.ActiveOnly) query = query.Where(s => s.ExpiresAt > now);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new AdminSessionDto(
                s.Id, s.UserId, s.User!.Name, s.User.Email,
                s.IpAddress, s.UserAgent, s.CreatedAt, s.LastSeenAt, s.ExpiresAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminSessionDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }
}

internal static class AdminUserProjection
{
    public static System.Linq.Expressions.Expression<Func<Domain.Entities.User, AdminUserDto>> Expression =>
        u => new AdminUserDto(
            u.Id, u.Email, u.Name, u.Image, u.Role, u.EmailVerified,
            u.Banned, u.BanReason, u.BanExpires, u.CreatedAt, u.UpdatedAt);
}
