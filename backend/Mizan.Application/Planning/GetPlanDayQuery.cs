using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// What the viewer's active plan says for one date, today by default. Their own
/// plan wins over a household plan; among several, the most recently changed.
/// Null when no active plan covers the date. With <c>PlanId</c>, the day of that
/// plan instead, whatever its status, so a draft can be read before it is
/// accepted; the date then defaults to the plan's first day.
/// </summary>
public record GetPlanDayQuery(DateOnly? Date, Guid? PlanId = null) : IRequest<PlanDayViewDto?>;

public class GetPlanDayQueryHandler : IRequestHandler<GetPlanDayQuery, PlanDayViewDto?>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IHouseholdAccess _households;
    private readonly IUserClock _clock;
    private readonly PlanReadModel _readModel;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public GetPlanDayQueryHandler(
        IMizanDbContext context, ICurrentUserService currentUser, IHouseholdAccess households, IUserClock clock, PlanReadModel readModel, PlanAccess access, HybridCache cache)
    {
        _access = access;
        _context = context;
        _currentUser = currentUser;
        _households = households;
        _clock = clock;
        _readModel = readModel;
        _cache = cache;
    }

    public async Task<PlanDayViewDto?> Handle(GetPlanDayQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        if (request.PlanId is { } planId) return await DayOfPlanAsync(planId, request.Date, userId, cancellationToken);
        var date = request.Date ?? await _clock.TodayAsync(userId, cancellationToken);
        var households = await _households.AccessibleIdsAsync(cancellationToken);

        var tags = new List<string>
        {
            CacheTags.MealPlansList(userId), CacheTags.Recipes, CacheTags.Foods, CacheTags.Nutrition(userId),
            CacheTags.Prices, CacheTags.PriceRegion(userId),
        };
        tags.AddRange(households.Select(CacheTags.HouseholdPlans));

        return await _cache.GetOrCreateAsync(
            $"plan-day:{userId}:{GrantContext.KeyFor(_currentUser.Grant)}:{date:yyyy-MM-dd}",
            (self: this, userId, date, households),
            static (state, ct) => state.self.LoadAsync(state.userId, state.date, state.households, ct),
            CacheOptions,
            tags,
            cancellationToken);
    }

    /// <summary>One day of a named plan. Uncached: it is read while a draft is being edited.</summary>
    private async Task<PlanDayViewDto?> DayOfPlanAsync(Guid planId, DateOnly? date, Guid userId, CancellationToken cancellationToken)
    {
        Domain.Entities.MealPlan plan;
        try
        {
            plan = await _access.LoadReadableAsync(planId, cancellationToken);
        }
        catch (Exceptions.EntityNotFoundException)
        {
            return null;
        }
        if (plan.Kind != PlanKind.Personal || plan.StartDate is not { } start) return null;

        var day = date ?? start;
        if (PlanSchedule.DayIndexOf(start, plan.DurationDays, day) is not { } dayIndex) return null;
        var built = await _readModel.BuildDayAsync(plan, dayIndex, userId, cancellationToken);
        return new PlanDayViewDto
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            Date = day,
            Day = built.Day,
            Currency = built.Currency,
            Rules = PlanRulesMapping.ToDto(plan.Rules),
        };
    }

    private async ValueTask<PlanDayViewDto?> LoadAsync(Guid userId, DateOnly date, IReadOnlyList<Guid> households, CancellationToken cancellationToken)
    {
        var candidates = await _context.MealPlans
            .Include(p => p.Entries)
            .Include(p => p.PrepSessions)
            .Where(p => p.Kind == PlanKind.Personal && p.Status == PlanStatus.Active && p.StartDate != null
                && p.StartDate <= date
                && (p.UserId == userId || (p.HouseholdId != null && households.Contains(p.HouseholdId.Value))))
            .OrderBy(p => p.UserId == userId && p.HouseholdId == null ? 0 : p.UserId == userId ? 1 : 2)
            .ThenByDescending(p => p.UpdatedAt)
            .ToListAsync(cancellationToken);

        var plan = candidates.FirstOrDefault(p => p.Covers(date));
        if (plan is null) return null;
        var dayIndex = PlanSchedule.DayIndexOf(plan.StartDate!.Value, plan.DurationDays, date)!.Value;
        var (day, currency) = await _readModel.BuildDayAsync(plan, dayIndex, userId, cancellationToken);
        return new PlanDayViewDto
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            Date = date,
            Day = day,
            Rules = PlanRulesMapping.ToDto(plan.Rules),
            Currency = currency,
        };
    }
}
