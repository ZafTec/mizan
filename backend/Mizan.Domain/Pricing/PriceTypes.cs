namespace Mizan.Domain.Pricing;

/// <summary>Where a price source gets its numbers.</summary>
public enum PriceSourceKind
{
    /// <summary>An administrator types prices in.</summary>
    Manual,

    /// <summary>The prices worker reads a website on a schedule.</summary>
    Scraper,

    /// <summary>Accepted user reports.</summary>
    Report,
}

/// <summary>
/// How much a price can be trusted. <see cref="PriceBook"/> prefers a newer
/// price of a higher confidence over any price of a lower one.
/// </summary>
public enum PriceConfidence
{
    /// <summary>Seen on a shelf or a product page.</summary>
    Observed = 2,

    /// <summary>A person reported it and an administrator accepted it.</summary>
    Reported = 1,

    /// <summary>A crowd average or a guess. Fills gaps only.</summary>
    Estimated = 0,
}

public enum PriceReportStatus
{
    Open,
    Accepted,
    Rejected,
}

/// <summary>
/// The units a price can be quoted in and how they become grams. Liquids are
/// treated as water (1 ml is 1 g); the catalogue has no densities, and the
/// error is smaller than the price spread between shops.
/// </summary>
public static class PriceUnits
{
    public const string Gram = "g";
    public const string Kilogram = "kg";
    public const string Millilitre = "ml";
    public const string Litre = "l";
    public const string Piece = "piece";

    public static readonly string[] All = [Gram, Kilogram, Millilitre, Litre, Piece];

    public static bool IsValid(string? unit) => unit is not null && All.Contains(Normalize(unit));

    public static string Normalize(string unit) => unit.Trim().ToLowerInvariant() switch
    {
        "g" or "gram" or "grams" => Gram,
        "kg" or "kilogram" or "kilograms" => Kilogram,
        "ml" or "millilitre" or "milliliter" or "millilitres" or "milliliters" => Millilitre,
        "l" or "litre" or "liter" or "litres" or "liters" => Litre,
        "piece" or "pieces" or "pc" or "pcs" or "each" or "unit" => Piece,
        var other => other,
    };

    /// <summary>
    /// Grams in <paramref name="quantity"/> of <paramref name="unit"/>. A piece
    /// needs the food's serving size in grams; without one the answer is null.
    /// </summary>
    public static decimal? Grams(decimal quantity, string unit, decimal? gramsPerPiece)
    {
        if (quantity <= 0) return null;
        return Normalize(unit) switch
        {
            Gram or Millilitre => quantity,
            Kilogram or Litre => quantity * 1000m,
            Piece => gramsPerPiece is > 0 ? quantity * gramsPerPiece : null,
            _ => null,
        };
    }
}

/// <summary>The region a person sees when they have not picked one, and the one the migration seeds.</summary>
public static class PriceRegionDefaults
{
    public const string Code = "ET-AA";
}
