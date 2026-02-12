using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetMcpTokensQuery : IRequest<PagedResult<McpTokenDto>>, IPagedQuery, ISortableQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record McpTokenDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public bool IsActive { get; init; }
}

public class GetMcpTokensQueryHandler : IRequestHandler<GetMcpTokensQuery, PagedResult<McpTokenDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMcpTokensQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<McpTokenDto>> Handle(GetMcpTokensQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new PagedResult<McpTokenDto>();
        }

        var query = _context.McpTokens
            .Where(t => t.UserId == _currentUser.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var tokens = await query
            .OrderByDescending(t => t.CreatedAt)
            .ApplyPaging(request)
            .Select(t => new McpTokenDto
            {
                Id = t.Id,
                Name = t.Name,
                CreatedAt = t.CreatedAt,
                ExpiresAt = t.ExpiresAt,
                LastUsedAt = t.LastUsedAt,
                IsActive = t.IsActive
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<McpTokenDto>
        {
            Items = tokens,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
