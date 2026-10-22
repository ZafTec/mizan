using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Application.Prices;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Planning;

/// <summary>
/// One list with its items, ordered by prep session and then by sort order,
/// each line costed at the viewer's region prices. Null for anyone who may not see it.
/// </summary>
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
    private readonly PriceBookReader _prices;
    private readonly HybridCache _cache;

    public GetShoppingListQueryHandler(
        IMizanDbContext context, ICurrentUserService currentUser, ShoppingListAccess access, PriceBookReader prices, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
        _prices = prices;
        _cache = cache;
    }

    public async Task<ShoppingListDetailDto?> Handle(GetShoppingListQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        return await _cache.GetOrCreateAsync(
            $"shopping-list:{request.Id}:{userId}:{GrantContext.KeyFor(_currentUser.Grant)}",
            (self: this, request, userId),
            static (state, ct) => state.self.LoadAsync(state.request, state.userId, ct),
            CacheOptions,
            tags: [CacheTags.ShoppingList(request.Id), CacheTags.Foods, CacheTags.Prices, CacheTags.PriceRegion(userId)],
            cancellationToken: cancellationToken);
    }

    private async ValueTask<ShoppingListDetailDto?> LoadAsync(GetShoppingListQuery request, Guid userId, CancellationToken cancellationToken)
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
        var foods = foodIds.Count == 0
            ? new Dictionary<Guid, Domain.Entities.Food>()
            : await _context.Foods.AsNoTracking().Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, cancellationToken);
        var region = await _prices.RegionForAsync(userId, cancellationToken);
        var quotes = await _prices.QuotesAsync(foods.Values, region, cancellationToken);
        var sessionIds = list.Items.Where(i => i.PrepSessionId.HasValue).Select(i => i.PrepSessionId!.Value).Distinct().ToList();
        var sessions = sessionIds.Count == 0
            ? new Dictionary<Guid, (int Day, string Title)>()
            : await _context.PrepSessions.AsNoTracking().Where(s => sessionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => (Day: s.DayIndex, Title: s.Title), cancellationToken);
        var planName = list.MealPlanId is { } planId
            ? await _context.MealPlans.AsNoTracking().Where(p => p.Id == planId).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        var ordered = list.Items
            .OrderBy(i => i.PrepSessionId.HasValue ? 0 : 1)
            .ThenBy(i => i.PrepSessionId is { } s && sessions.TryGetValue(s, out var session) ? session.Day : int.MaxValue)
            .ThenBy(i => i.PrepSessionId)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        // A line whose food left the catalogue is costed as a line without a food.
        var cost = PlanCostCalculator.Cost(
            ordered.Select(i => new ShoppingDraftItem(
                i.FoodId is { } f && foods.ContainsKey(f) ? f : null, i.ItemName, i.Grams, i.Amount, i.Unit, i.NeedsReview, i.PrepSessionId, 1)),
            quotes,
            region.Currency);

        var items = ordered
            .Select((i, index) => new ShoppingListItemDto
            {
                Id = i.Id,
                FoodId = i.FoodId,
                FoodName = i.FoodId is { } f && foods.TryGetValue(f, out var food) ? food.Name : null,
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
                Cost = cost.Items[index].Cost,
                CostConfidence = cost.Items[index].Confidence?.ToString(),
                UnpricedReason = cost.Items[index].Reason,
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
            Currency = region.Currency,
            TotalCost = PlanCosting.PricedTotal(cost),
            CostCoverage = cost.Coverage,
            CostConfidence = cost.LowestConfidence?.ToString(),
            UnpricedCount = cost.Unpriced.Count(),
            CreatedAt = list.CreatedAt,
            UpdatedAt = list.UpdatedAt,
        };
    }
}
