using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Planning;

/// <summary>The lists a person owns and the ones shared in households they can see, newest change first.</summary>
public record GetShoppingListsQuery : IRequest<PagedResult<ShoppingListSummaryDto>>, IPagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetShoppingListsQueryHandler : IRequestHandler<GetShoppingListsQuery, PagedResult<ShoppingListSummaryDto>>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IHouseholdAccess _households;
    private readonly HybridCache _cache;

    public GetShoppingListsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, IHouseholdAccess households, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _households = households;
        _cache = cache;
    }

    public async Task<PagedResult<ShoppingListSummaryDto>> Handle(GetShoppingListsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        var households = await _households.AccessibleIdsAsync(cancellationToken);

        var tags = new List<string> { CacheTags.ShoppingLists(userId) };
        tags.AddRange(households.Select(CacheTags.HouseholdShoppingLists));

        return await _cache.GetOrCreateAsync(
            $"shopping-lists:{userId}:{GrantContext.KeyFor(_currentUser.Grant)}:{request.Page}:{request.PageSize}",
            (self: this, request, userId, households),
            static (state, ct) => state.self.LoadAsync(state.request, state.userId, state.households, ct),
            CacheOptions,
            tags,
            cancellationToken);
    }

    private async ValueTask<PagedResult<ShoppingListSummaryDto>> LoadAsync(
        GetShoppingListsQuery request, Guid userId, IReadOnlyList<Guid> households, CancellationToken cancellationToken)
    {
        // A connected app sees only the households it was given; a household
        // list the owner filed under one it cannot see is withheld even from them.
        var restricted = _currentUser.Grant is not null;
        var query = _context.ShoppingLists.AsNoTracking()
            .Where(l => (l.UserId == userId && (!restricted || l.HouseholdId == null || households.Contains(l.HouseholdId.Value)))
                || (l.HouseholdId != null && households.Contains(l.HouseholdId.Value)));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(l => l.UpdatedAt)
            .ApplyPaging(request)
            .Select(l => new ShoppingListSummaryDto
            {
                Id = l.Id,
                Name = l.Name,
                UserId = l.UserId,
                HouseholdId = l.HouseholdId,
                MealPlanId = l.MealPlanId,
                GeneratedAt = l.GeneratedAt,
                TotalItems = l.Items.Count,
                CheckedItems = l.Items.Count(i => i.IsChecked),
                NeedsReviewCount = l.Items.Count(i => i.NeedsReview),
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ShoppingListSummaryDto> { Items = items, TotalCount = totalCount, Page = request.Page, PageSize = request.PageSize };
    }
}
