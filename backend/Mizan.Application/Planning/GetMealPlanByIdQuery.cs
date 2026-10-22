using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Planning;

/// <summary>One plan or template with its days, totals, violations and the viewer's logged flags. "Not found" for anyone who may not see it.</summary>
public record GetMealPlanByIdQuery(Guid Id) : IRequest<MealPlanDetailDto?>;

public class GetMealPlanByIdQueryHandler : IRequestHandler<GetMealPlanByIdQuery, MealPlanDetailDto?>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private readonly PlanAccess _access;
    private readonly PlanReadModel _readModel;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public GetMealPlanByIdQueryHandler(PlanAccess access, PlanReadModel readModel, ICurrentUserService currentUser, HybridCache cache)
    {
        _access = access;
        _readModel = readModel;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<MealPlanDetailDto?> Handle(GetMealPlanByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");

        // The viewer is part of the key: the logged flags are theirs, and a
        // stranger gets null. The tags are the plan's own, the catalogues it
        // resolves through, and the viewer's diary, so an edit by anyone, a
        // recipe change, or a log by the viewer clears this copy. Costs are in
        // the viewer's region, so a stored price or a region switch clears it too.
        return await _cache.GetOrCreateAsync(
            $"mealplan:{request.Id}:{userId}:{GrantContext.KeyFor(_currentUser.Grant)}",
            (self: this, request, userId),
            static (state, ct) => state.self.LoadAsync(state.request, state.userId, ct),
            CacheOptions,
            tags: [CacheTags.MealPlan(request.Id), CacheTags.Recipes, CacheTags.Foods, CacheTags.Nutrition(userId), CacheTags.Prices, CacheTags.PriceRegion(userId)],
            cancellationToken: cancellationToken);
    }

    private async ValueTask<MealPlanDetailDto?> LoadAsync(GetMealPlanByIdQuery request, Guid userId, CancellationToken cancellationToken)
    {
        Domain.Entities.MealPlan plan;
        try
        {
            plan = await _access.LoadReadableAsync(request.Id, cancellationToken);
        }
        catch (Exceptions.EntityNotFoundException)
        {
            return null;
        }
        var canEdit = await _access.CanEditAsync(plan, cancellationToken);
        return await _readModel.BuildAsync(plan, userId, canEdit, cancellationToken);
    }
}
