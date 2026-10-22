namespace Mizan.Contracts.Prices;

/// <summary>
/// GET /api/Foods/{id}/price: the price in force for the viewer's region and
/// where it came from. <c>Priced</c> is false and the price fields are null
/// when the region has no usable price for the food.
/// </summary>
public record FoodPriceQuoteDto
{
    public Guid FoodId { get; init; }
    public string RegionCode { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public bool Priced { get; init; }
    public decimal? PricePerGram { get; init; }
    public decimal? PricePerKg { get; init; }

    /// <summary>The price as quoted: <c>Price</c> for <c>Quantity</c> <c>Unit</c>, for example 120 for 1 kg.</summary>
    public decimal? Price { get; init; }
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; }

    /// <summary>Observed, Reported or Estimated.</summary>
    public string? Confidence { get; init; }
    public DateTime? ObservedAt { get; init; }
    public Guid? SourceId { get; init; }
    public string? SourceName { get; init; }
}

/// <summary>Body of POST /api/Foods/{id}/price-reports: what the person paid, for how much of the food. <c>Unit</c> is g, kg, ml, l or piece.</summary>
public record CreatePriceReportRequest(decimal SuggestedPrice, decimal Quantity, string Unit, string? Note = null);

public record PriceReportCreatedResult(Guid Id);
