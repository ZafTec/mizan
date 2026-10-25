using Mizan.Domain.Pricing;

namespace Mizan.Domain.Entities;

/// <summary>One price of one food in one region at one time. Never updated; a new observation is a new row.</summary>
public class FoodPrice
{
    public Guid Id { get; set; }
    public Guid FoodId { get; set; }
    public string RegionCode { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }

    /// <summary>What <see cref="Quantity"/> <see cref="Unit"/> of the food costs, in the region's currency.</summary>
    public decimal Price { get; set; }
    public decimal Quantity { get; set; } = 1;

    /// <summary>A <see cref="PriceUnits"/> value.</summary>
    public string Unit { get; set; } = PriceUnits.Kilogram;

    public PriceConfidence Confidence { get; set; } = PriceConfidence.Observed;
    public DateTime ObservedAt { get; set; }

    /// <summary>The page the price was read from, when there is one.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>The person whose report this price came from, when it did.</summary>
    public Guid? ReportedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Food Food { get; set; } = null!;
    public virtual PriceRegion Region { get; set; } = null!;
    public virtual PriceSource? Source { get; set; }
}
