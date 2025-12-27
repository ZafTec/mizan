using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetShoppingListsQuery : IRequest<GetShoppingListsResult>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record GetShoppingListsResult
{
    public List<ShoppingListSummaryDto> ShoppingLists { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
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

public class GetShoppingListsQueryHandler : IRequestHandler<GetShoppingListsQuery, GetShoppingListsResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetShoppingListsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetShoppingListsResult> Handle(GetShoppingListsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var query = _context.ShoppingLists
            .Include(sl => sl.Items)
            .Where(sl => sl.UserId == _currentUser.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var shoppingLists = await query
            .OrderByDescending(sl => sl.UpdatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
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

        return new GetShoppingListsResult
        {
            ShoppingLists = shoppingLists,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
