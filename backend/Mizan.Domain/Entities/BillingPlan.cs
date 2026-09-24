namespace Mizan.Domain.Entities;

/// <summary>
/// A price Mizan sells Pro at. Paddle holds the price itself; this row is the
/// catalogue the pricing page, the billing page, and checkout read, so an
/// admin can change what is on sale without a rebuild.
///
/// A Paddle price's amount is fixed once subscribers pay it, so a new amount is
/// a new plan: the old row is archived and keeps its Paddle price, which is
/// what its existing subscribers go on renewing at.
/// </summary>
public class BillingPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>"month" or "year". Pro is the only product, so the interval is what tells plans apart.</summary>
    public string Interval { get; set; } = BillingIntervals.Month;

    /// <summary>In the currency's lowest unit: cents for USD.</summary>
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "USD";
    public int? TrialDays { get; set; }

    public string PaddleProductId { get; set; } = string.Empty;
    public string PaddlePriceId { get; set; } = string.Empty;

    /// <summary>On sale. Archived plans stay so their subscribers still see what they pay.</summary>
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
}

public static class BillingIntervals
{
    public const string Month = "month";
    public const string Year = "year";

    public static bool IsValid(string? interval) => interval is Month or Year;
}
