using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetAuditLogsQuery : IRequest<PagedResult<AuditLogDto>>, IPagedQuery, ISortableQuery
{
    public string? Action { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public Guid? UserId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record AuditLogDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? Details { get; init; }
    public string? IpAddress { get; init; }
    public DateTime Timestamp { get; set; }
}

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private static readonly Dictionary<string, Expression<Func<Domain.Entities.AuditLog, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["timestamp"] = a => a.Timestamp,
        ["action"] = a => a.Action,
        ["entitytype"] = a => a.EntityType
    };

    private readonly IMizanDbContext _context;

    public GetAuditLogsQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Action))
        {
            query = query.Where(a => a.Action.Contains(request.Action));
        }

        if (!string.IsNullOrEmpty(request.EntityType))
        {
            query = query.Where(a => a.EntityType == request.EntityType);
        }

        if (!string.IsNullOrEmpty(request.EntityId))
        {
            query = query.Where(a => a.EntityId == request.EntityId);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == request.UserId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortedQuery = query.ApplySorting(
            request,
            SortMappings,
            defaultSort: a => a.Timestamp,
            defaultDescending: true);

        var logs = await sortedQuery
            .ApplyPaging(request)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserEmail = a.User != null ? a.User.Email : null,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                Timestamp = a.Timestamp
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>
        {
            Items = logs,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
