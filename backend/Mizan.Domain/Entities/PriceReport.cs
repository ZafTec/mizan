using Mizan.Domain.Pricing;

namespace Mizan.Domain.Entities;

/// <summary>A person says a shown price is wrong and offers the one they paid. An administrator accepts or rejects it.</summary>
public class PriceReport
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FoodId { get; set; }
    public string RegionCode { get; set; } = string.Empty;

    public decimal SuggestedPrice { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string Unit { get; set; } = PriceUnits.Kilogram;
    public string? Note { get; set; }

    /// <summary>The price the person saw when they reported, so the review shows the difference.</summary>
    public decimal? ShownPricePerGram { get; set; }

    public PriceReportStatus Status { get; set; } = PriceReportStatus.Open;
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Food Food { get; set; } = null!;
    public virtual PriceRegion Region { get; set; } = null!;
    public virtual User? ResolvedBy { get; set; }
}
