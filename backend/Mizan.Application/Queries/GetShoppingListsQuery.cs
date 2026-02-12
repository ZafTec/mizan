using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetShoppingListsQuery : IRequest<PagedResult<ShoppingListSummaryDto>>, IPagedQuery, ISortableQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record ShoppingListSummaryDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public int TotalItems { get; init; }
    public int CheckedItems { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public class GetShoppingListsQueryHandler : IRequestHandler<GetShoppingListsQuery, PagedResult<ShoppingListSummaryDto>>
{
    private static readonly Dictionary<string, Expression<Func<Domain.Entities.ShoppingList, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = sl => sl.Name!,
        ["updatedat"] = sl => sl.UpdatedAt,
        ["createdat"] = sl => sl.CreatedAt
    };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetShoppingListsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ShoppingListSummaryDto>> Handle(GetShoppingListsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var query = _context.ShoppingLists
            .Include(sl => sl.Items)
            .Where(sl => sl.UserId == _currentUser.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var sortedQuery = query.ApplySorting(
            request,
            SortMappings,
            defaultSort: sl => sl.UpdatedAt,
            defaultDescending: true);

        var shoppingLists = await sortedQuery
            .ApplyPaging(request)
            .Select(sl => new ShoppingListSummaryDto
            {
                Id = sl.Id,
                Name = sl.Name,
                TotalItems = sl.Items.Count,
                CheckedItems = sl.Items.Count(i => i.IsChecked),
                CreatedAt = sl.CreatedAt,
                UpdatedAt = sl.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ShoppingListSummaryDto>
        {
            Items = shoppingLists,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
