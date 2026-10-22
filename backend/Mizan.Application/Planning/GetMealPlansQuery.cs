using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// The plans a person follows or can open: the ones they own, the ones shared
/// in their households, and the drafts they authored as a coach. Templates
/// have their own listing. <c>Status</c> filters by name; the default shows
/// everything, active first.
/// </summary>
public record GetMealPlansQuery : IRequest<PagedResult<MealPlanSummaryDto>>, IPagedQuery
{
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetMealPlansQueryHandler : IRequestHandler<GetMealPlansQuery, PagedResult<MealPlanSummaryDto>>
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

    public GetMealPlansQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, IHouseholdAccess households, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _households = households;
        _cache = cache;
    }

    public async Task<PagedResult<MealPlanSummaryDto>> Handle(GetMealPlansQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        PlanStatus? status = string.IsNullOrWhiteSpace(request.Status) ? null : PlanRulesMapping.ParseEnum(request.Status, PlanStatus.Active);
        var households = await _households.AccessibleIdsAsync(cancellationToken);

        // The viewer's own tag plus one per household they can see, so a write
        // to a shared plan by another member clears this listing too.
        var key = $"mealplans:{userId}:{GrantContext.KeyFor(_currentUser.Grant)}:{status}:{request.Page}:{request.PageSize}";
        var tags = new List<string> { CacheTags.MealPlansList(userId) };
        tags.AddRange(households.Select(CacheTags.HouseholdPlans));

        return await _cache.GetOrCreateAsync(
            key,
            (self: this, request, userId, households, status),
            static (state, ct) => state.self.LoadAsync(state.request, state.userId, state.households, state.status, ct),
            CacheOptions,
            tags,
            cancellationToken);
    }

    private async ValueTask<PagedResult<MealPlanSummaryDto>> LoadAsync(
        GetMealPlansQuery request, Guid userId, IReadOnlyList<Guid> households, PlanStatus? status, CancellationToken cancellationToken)
    {
        var query = _context.MealPlans.AsNoTracking()
            .Where(p => p.Kind == PlanKind.Personal)
            .Where(p => p.UserId == userId
                || (p.HouseholdId != null && households.Contains(p.HouseholdId.Value))
                || (p.AuthoredByUserId == userId && p.Status == PlanStatus.Draft));
        if (status is { } s) query = query.Where(p => p.Status == s);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(p => p.Status == PlanStatus.Active ? 0 : p.Status == PlanStatus.Draft ? 1 : 2)
            .ThenByDescending(p => p.StartDate)
            .ThenByDescending(p => p.CreatedAt)
            .ApplyPaging(request)
            .Select(p => new
            {
                Plan = p,
                EntryCount = p.Entries.Count,
                AuthorName = p.AuthoredBy != null ? p.AuthoredBy.Name : null,
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new MealPlanSummaryDto
        {
            Id = r.Plan.Id,
            Name = r.Plan.Name,
            Description = r.Plan.Description,
            Kind = r.Plan.Kind.ToString(),
            Status = r.Plan.Status.ToString(),
            StartDate = r.Plan.StartDate,
            EndDate = r.Plan.EndDate,
            DurationDays = r.Plan.DurationDays,
            UserId = r.Plan.UserId,
            HouseholdId = r.Plan.HouseholdId,
            AuthoredByUserId = r.Plan.AuthoredByUserId,
            AuthoredByName = r.AuthorName,
            EntryCount = r.EntryCount,
            HasRules = r.Plan.Rules.HasAnyRule,
            CreatedAt = r.Plan.CreatedAt,
            UpdatedAt = r.Plan.UpdatedAt,
        }).ToList();

        return new PagedResult<MealPlanSummaryDto> { Items = items, TotalCount = totalCount, Page = request.Page, PageSize = request.PageSize };
    }
}
