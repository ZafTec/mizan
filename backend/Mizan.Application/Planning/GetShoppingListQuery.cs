using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Planning;

/// <summary>One list with its items, ordered by prep session and then by sort order. Null for anyone who may not see it.</summary>
public record GetShoppingListQuery(Guid Id) : IRequest<ShoppingListDetailDto?>;

public class GetShoppingListQueryHandler : IRequestHandler<GetShoppingListQuery, ShoppingListDetailDto?>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ShoppingListAccess _access;
    private readonly HybridCache _cache;

    public GetShoppingListQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, ShoppingListAccess access, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
        _cache = cache;
    }

    public async Task<ShoppingListDetailDto?> Handle(GetShoppingListQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        return await _cache.GetOrCreateAsync(
            $"shopping-list:{request.Id}:{userId}:{GrantContext.KeyFor(_currentUser.Grant)}",
            (self: this, request),
            static (state, ct) => state.self.LoadAsync(state.request, ct),
            CacheOptions,
            tags: [CacheTags.ShoppingList(request.Id), CacheTags.Foods],
            cancellationToken: cancellationToken);
    }

    private async ValueTask<ShoppingListDetailDto?> LoadAsync(GetShoppingListQuery request, CancellationToken cancellationToken)
    {
        Domain.Entities.ShoppingList list;
        try
        {
            list = await _access.LoadReadableAsync(request.Id, cancellationToken);
        }
        catch (Exceptions.EntityNotFoundException)
        {
            return null;
        }

        var foodIds = list.Items.Where(i => i.FoodId.HasValue).Select(i => i.FoodId!.Value).Distinct().ToList();
        var foodNames = foodIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Foods.AsNoTracking().Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, cancellationToken);
        var sessionIds = list.Items.Where(i => i.PrepSessionId.HasValue).Select(i => i.PrepSessionId!.Value).Distinct().ToList();
        var sessions = sessionIds.Count == 0
            ? new Dictionary<Guid, (int Day, string Title)>()
            : await _context.PrepSessions.AsNoTracking().Where(s => sessionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => (Day: s.DayIndex, Title: s.Title), cancellationToken);
        var planName = list.MealPlanId is { } planId
            ? await _context.MealPlans.AsNoTracking().Where(p => p.Id == planId).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        var items = list.Items
            .OrderBy(i => i.PrepSessionId.HasValue ? 0 : 1)
            .ThenBy(i => i.PrepSessionId is { } s && sessions.TryGetValue(s, out var session) ? session.Day : int.MaxValue)
            .ThenBy(i => i.PrepSessionId)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
            .Select(i => new ShoppingListItemDto
            {
                Id = i.Id,
                FoodId = i.FoodId,
                FoodName = i.FoodId is { } f && foodNames.TryGetValue(f, out var name) ? name : null,
                ItemName = i.ItemName,
                Amount = i.Amount,
                Unit = i.Unit,
                Grams = i.Grams,
                Category = i.Category,
                NeedsReview = i.NeedsReview,
                PrepSessionId = i.PrepSessionId,
                PrepSessionTitle = i.PrepSessionId is { } p && sessions.TryGetValue(p, out var ps) ? ps.Title : null,
                SortOrder = i.SortOrder,
                IsChecked = i.IsChecked,
                Cost = null,
            })
            .ToList();

        return new ShoppingListDetailDto
        {
            Id = list.Id,
            Name = list.Name,
            UserId = list.UserId,
            HouseholdId = list.HouseholdId,
            MealPlanId = list.MealPlanId,
            MealPlanName = planName,
            GeneratedAt = list.GeneratedAt,
            Items = items,
            TotalItems = items.Count,
            CheckedItems = items.Count(i => i.IsChecked),
            NeedsReviewCount = items.Count(i => i.NeedsReview),
            CanEdit = await _access.CanEditAsync(list, cancellationToken),
            TotalCost = null,
            CreatedAt = list.CreatedAt,
            UpdatedAt = list.UpdatedAt,
        };
    }
}
