using Mizan.Domain.Entities;

namespace Mizan.Application.Billing;

/// <summary>A plan as the pricing and billing pages show it, with the deal checkout will apply.</summary>
public record BillingPlanDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Interval { get; init; } = BillingIntervals.Month;
    public int AmountCents { get; init; }
    public string Currency { get; init; } = "USD";
    public int? TrialDays { get; init; }
    public string PaddlePriceId { get; init; } = string.Empty;
    public BillingDealDto? Deal { get; init; }
}

/// <summary>An automatic discount, passed to checkout by id and advertised by label.</summary>
public record BillingDealDto
{
    public string PaddleDiscountId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Type { get; init; } = BillingDiscountTypes.Percentage;
    public decimal Amount { get; init; }
    public bool Recurring { get; init; }
    public int? MaximumRecurringIntervals { get; init; }
    public DateTime? ExpiresAt { get; init; }

    /// <summary>The first payment after the discount, in cents.</summary>
    public int DiscountedAmountCents { get; init; }
}

public record AdminBillingPlanDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Interval { get; init; } = BillingIntervals.Month;
    public int AmountCents { get; init; }
    public string Currency { get; init; } = "USD";
    public int? TrialDays { get; init; }
    public string PaddleProductId { get; init; } = string.Empty;
    public string PaddlePriceId { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ArchivedAt { get; init; }

    /// <summary>Subscriptions currently billed at this plan's price, including those ending at period end.</summary>
    public int Subscribers { get; init; }
}

public record AdminBillingDiscountDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string Type { get; init; } = BillingDiscountTypes.Percentage;
    public decimal Amount { get; init; }
    public bool Recurring { get; init; }
    public int? MaximumRecurringIntervals { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public IReadOnlyList<Guid> PlanIds { get; init; } = Array.Empty<Guid>();
    public string PaddleDiscountId { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record AdminBillingCatalogDto
{
    public IReadOnlyList<AdminBillingPlanDto> Plans { get; init; } = Array.Empty<AdminBillingPlanDto>();
    public IReadOnlyList<AdminBillingDiscountDto> Discounts { get; init; } = Array.Empty<AdminBillingDiscountDto>();

    /// <summary>"sandbox" or "production", so an admin knows which Paddle account the buttons act on.</summary>
    public string Environment { get; init; } = "sandbox";
    public bool Configured { get; init; }
}

public static class BillingPricing
{
    /// <summary>The best automatic deal for a plan: the one that takes most off the first payment.</summary>
    public static BillingDealDto? DealFor(BillingPlan plan, IEnumerable<BillingDiscount> discounts, DateTime now)
    {
        return discounts
            .Where(d => d.IsAutomatic && d.IsAvailable(now) && d.AppliesTo(plan.Id))
            .Where(d => d.Type == BillingDiscountTypes.Percentage || d.Currency == plan.Currency)
            .Select(d => new BillingDealDto
            {
                PaddleDiscountId = d.PaddleDiscountId,
                Label = d.Label,
                Type = d.Type,
                Amount = d.Amount,
                Recurring = d.Recurring,
                MaximumRecurringIntervals = d.MaximumRecurringIntervals,
                ExpiresAt = d.ExpiresAt,
                DiscountedAmountCents = Discounted(plan.AmountCents, d)
            })
            .OrderBy(d => d.DiscountedAmountCents)
            .FirstOrDefault();
    }

    public static int Discounted(int amountCents, BillingDiscount discount) =>
        discount.Type == BillingDiscountTypes.Percentage
            ? (int)Math.Round(amountCents * (100m - discount.Amount) / 100m, MidpointRounding.AwayFromZero)
            : Math.Max(0, amountCents - (int)discount.Amount);
}
