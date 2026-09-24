namespace Mizan.Domain.Entities;

/// <summary>
/// A Paddle discount an admin created from Mizan. With no code it is a deal:
/// checkout applies it automatically and the pricing page advertises it. With a
/// code it is only applied when a customer enters the code at checkout.
/// </summary>
public class BillingDiscount
{
    public Guid Id { get; set; }

    /// <summary>What the pricing page calls it: "Launch offer".</summary>
    public string Label { get; set; } = string.Empty;
    public string? Code { get; set; }

    /// <summary>"percentage" or "flat".</summary>
    public string Type { get; set; } = BillingDiscountTypes.Percentage;

    /// <summary>A percentage (for example 20) or, for flat discounts, cents off.</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";

    /// <summary>Applies to renewals too, not only the first payment.</summary>
    public bool Recurring { get; set; }

    /// <summary>With <see cref="Recurring"/>: how many billing periods it lasts. Null means for as long as the subscription runs.</summary>
    public int? MaximumRecurringIntervals { get; set; }
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Plans it applies to. Empty means every plan.</summary>
    public List<Guid> PlanIds { get; set; } = new();

    public string PaddleDiscountId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsAutomatic => string.IsNullOrEmpty(Code);

    public bool IsAvailable(DateTime now) => IsActive && (ExpiresAt is null || ExpiresAt > now);

    public bool AppliesTo(Guid planId) => PlanIds.Count == 0 || PlanIds.Contains(planId);
}

public static class BillingDiscountTypes
{
    public const string Percentage = "percentage";
    public const string Flat = "flat";

    public static bool IsValid(string? type) => type is Percentage or Flat;
}
