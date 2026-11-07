using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Ai;
using Mizan.Application.Common;
using Mizan.Application.Features;
using Mizan.Application.Interfaces;
using Mizan.Domain.Usage;

namespace Mizan.Application.Usage;

/// <summary>An override as cached: the table is small and read on every reservation.</summary>
public sealed record CachedUsageLimit(UsageLimitScope Scope, Guid SubjectId, string? Line, UsageLimitValues Values);

public sealed class UsageLimitService : IUsageLimits
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(1),
        LocalCacheExpiration = TimeSpan.FromSeconds(30),
    };

    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;
    private readonly IEntitlementService _entitlements;
    private readonly IAiTierDefaults _tiers;
    private readonly ActiveHousehold _households;

    public UsageLimitService(
        IMizanDbContext context,
        HybridCache cache,
        IEntitlementService entitlements,
        IAiTierDefaults tiers,
        ActiveHousehold households)
    {
        _context = context;
        _cache = cache;
        _entitlements = entitlements;
        _tiers = tiers;
        _households = households;
    }

    public async Task<EffectiveUsageLimits> ResolveAsync(
        Guid userId, Guid? householdId, AiQuotaLine line, CancellationToken cancellationToken = default)
    {
        var entitlement = await _entitlements.GetAsync(userId, cancellationToken);
        var household = householdId ?? await _households.ForUserAsync(userId, cancellationToken);
        var plan = entitlement.IsPro ? await CurrentPlanAsync(userId, cancellationToken) : null;
        var overrides = await AllAsync(cancellationToken);

        var layers = Layers(overrides, line, (UsageLimitScope.User, userId), (UsageLimitScope.Household, household), (UsageLimitScope.Plan, plan));
        return new EffectiveUsageLimits(
            UsageLimitComposition.Compose(await _tiers.ForAsync(line, entitlement.IsPro, cancellationToken), layers),
            household,
            plan,
            entitlement.IsPro);
    }

    /// <summary>The catalogue plan the user's subscription is on, matched by its Paddle price.</summary>
    public Task<Guid?> CurrentPlanAsync(Guid userId, CancellationToken cancellationToken) =>
        _context.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == userId && s.PaddlePriceId != null)
            .Join(_context.BillingPlans.AsNoTracking(), s => s.PaddlePriceId, p => p.PaddlePriceId, (s, p) => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<CachedUsageLimit>> AllAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync(
            "usage-limits:all",
            async ct => await _context.UsageLimitOverrides.AsNoTracking()
                .Select(o => new CachedUsageLimit(o.Scope, o.SubjectId, o.Line,
                    new UsageLimitValues(o.DailyRequests, o.DailyTokens, o.DailyCostMicros, o.MonthlyCostMicros)))
                .ToListAsync(ct),
            CacheOptions,
            [CacheTags.UsageLimits],
            cancellationToken).AsTask();

    /// <summary>
    /// The layers for one line, most specific first. Each scope contributes its
    /// row for the line, then its row for every line, so a line override can
    /// tighten one cap and leave the rest to the all-lines row.
    /// </summary>
    public static IReadOnlyList<UsageLimitLayer> Layers(
        IReadOnlyCollection<CachedUsageLimit> overrides,
        AiQuotaLine line,
        params (UsageLimitScope Scope, Guid? SubjectId)[] subjects)
    {
        var name = line.ToString();
        var layers = new List<UsageLimitLayer>();
        foreach (var (scope, subject) in subjects)
        {
            if (subject is not { } id) continue;
            var source = SourceOf(scope);
            foreach (var match in new[] { name, null })
            {
                var row = overrides.FirstOrDefault(o => o.Scope == scope && o.SubjectId == id && o.Line == match);
                if (row is not null) layers.Add(new UsageLimitLayer(source, row.Values));
            }
        }

        return layers;
    }

    public static UsageLimitSource SourceOf(UsageLimitScope scope) => scope switch
    {
        UsageLimitScope.User => UsageLimitSource.User,
        UsageLimitScope.Household => UsageLimitSource.Household,
        _ => UsageLimitSource.Plan,
    };
}
