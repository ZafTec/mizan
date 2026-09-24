using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Billing;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

/// <summary>
/// What Pro costs right now: the plans on sale and any deal checkout applies.
/// Public - the landing page reads it before anyone signs in. Cached for every
/// viewer alike, because it holds nothing about the viewer.
/// </summary>
public record GetBillingPlansQuery : IRequest<IReadOnlyList<BillingPlanDto>>;

public class GetBillingPlansQueryHandler : IRequestHandler<GetBillingPlansQuery, IReadOnlyList<BillingPlanDto>>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        // A deal's expiry is honoured within this window; Paddle rejects an
        // expired discount at checkout regardless.
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };

    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public GetBillingPlansQueryHandler(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IReadOnlyList<BillingPlanDto>> Handle(GetBillingPlansQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            "billing-plans",
            LoadAsync,
            CacheOptions,
            tags: new[] { CacheTags.BillingPlans },
            cancellationToken: cancellationToken);
    }

    private async ValueTask<IReadOnlyList<BillingPlanDto>> LoadAsync(CancellationToken cancellationToken)
    {
        var plans = await _context.BillingPlans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.AmountCents)
            .ToListAsync(cancellationToken);

        var discounts = await _context.BillingDiscounts.AsNoTracking()
            .Where(d => d.IsActive && d.Code == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        return plans.Select(p => new BillingPlanDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Interval = p.Interval,
            AmountCents = p.AmountCents,
            Currency = p.Currency,
            TrialDays = p.TrialDays,
            PaddlePriceId = p.PaddlePriceId,
            Deal = BillingPricing.DealFor(p, discounts, now)
        }).ToList();
    }
}
