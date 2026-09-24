using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mizan.Application.Billing;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

/// <summary>Every plan and discount, archived ones included, with how many subscribers each plan bills.</summary>
public record GetAdminBillingCatalogQuery : IRequest<AdminBillingCatalogDto>;

public class GetAdminBillingCatalogQueryHandler : IRequestHandler<GetAdminBillingCatalogQuery, AdminBillingCatalogDto>
{
    private static readonly string[] BilledStatuses = { "active", "trialing", "past_due", "paused" };

    private readonly IMizanDbContext _context;
    private readonly PaddleOptions _options;

    public GetAdminBillingCatalogQueryHandler(IMizanDbContext context, IOptions<PaddleOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<AdminBillingCatalogDto> Handle(GetAdminBillingCatalogQuery request, CancellationToken cancellationToken)
    {
        var subscribers = await _context.Subscriptions.AsNoTracking()
            .Where(s => s.PaddlePriceId != null && BilledStatuses.Contains(s.Status))
            .GroupBy(s => s.PaddlePriceId!)
            .Select(g => new { PriceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PriceId, x => x.Count, cancellationToken);

        var plans = await _context.BillingPlans.AsNoTracking()
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.SortOrder).ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var discounts = await _context.BillingDiscounts.AsNoTracking()
            .OrderByDescending(d => d.IsActive).ThenByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return new AdminBillingCatalogDto
        {
            Environment = _options.Environment,
            Configured = !string.IsNullOrWhiteSpace(_options.ApiKey),
            Plans = plans.Select(p => new AdminBillingPlanDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Interval = p.Interval,
                AmountCents = p.AmountCents,
                Currency = p.Currency,
                TrialDays = p.TrialDays,
                PaddleProductId = p.PaddleProductId,
                PaddlePriceId = p.PaddlePriceId,
                IsActive = p.IsActive,
                SortOrder = p.SortOrder,
                CreatedAt = p.CreatedAt,
                ArchivedAt = p.ArchivedAt,
                Subscribers = subscribers.GetValueOrDefault(p.PaddlePriceId)
            }).ToList(),
            Discounts = discounts.Select(d => new AdminBillingDiscountDto
            {
                Id = d.Id,
                Label = d.Label,
                Code = d.Code,
                Type = d.Type,
                Amount = d.Amount,
                Recurring = d.Recurring,
                MaximumRecurringIntervals = d.MaximumRecurringIntervals,
                ExpiresAt = d.ExpiresAt,
                PlanIds = d.PlanIds,
                PaddleDiscountId = d.PaddleDiscountId,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt
            }).ToList()
        };
    }
}
