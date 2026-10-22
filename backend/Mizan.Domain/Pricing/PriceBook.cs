using Mizan.Domain.Entities;

namespace Mizan.Domain.Pricing;

/// <summary>The price in force for one food in one region, per gram.</summary>
public sealed record FoodPriceQuote(
    Guid FoodId,
    string RegionCode,
    string Currency,
    decimal PricePerGram,
    PriceConfidence Confidence,
    DateTime ObservedAt,
    Guid? SourceId,
    string? SourceName);

/// <summary>
/// Picks the price for a food and region: the newest Observed price, else the
/// newest Reported, else the newest Estimated. Pure: callers load the
/// candidate rows.
/// </summary>
public static class PriceBook
{
    public static FoodPriceQuote? Quote(
        Food food,
        string regionCode,
        string currency,
        IEnumerable<FoodPrice> candidates,
        IReadOnlyDictionary<Guid, string>? sourceNames = null)
    {
        FoodPrice? best = null;
        foreach (var price in candidates)
        {
            if (price.FoodId != food.Id) continue;
            if (!string.Equals(price.RegionCode, regionCode, StringComparison.OrdinalIgnoreCase)) continue;
            if (PerGram(price, food) is null) continue;
            if (best is null
                || price.Confidence > best.Confidence
                || (price.Confidence == best.Confidence && price.ObservedAt > best.ObservedAt))
            {
                best = price;
            }
        }

        if (best is null) return null;
        var perGram = PerGram(best, food)!.Value;
        string? name = null;
        if (best.SourceId is { } sid && sourceNames is not null) sourceNames.TryGetValue(sid, out name);
        return new FoodPriceQuote(food.Id, regionCode, currency, perGram, best.Confidence, best.ObservedAt, best.SourceId, name);
    }

    /// <summary>Quotes for many foods at once, keyed by food id. Foods without a usable price are absent.</summary>
    public static IReadOnlyDictionary<Guid, FoodPriceQuote> QuoteAll(
        IEnumerable<Food> foods,
        string regionCode,
        string currency,
        IEnumerable<FoodPrice> candidates,
        IReadOnlyDictionary<Guid, string>? sourceNames = null)
    {
        var byFood = candidates.GroupBy(p => p.FoodId).ToDictionary(g => g.Key, g => g.ToList());
        var quotes = new Dictionary<Guid, FoodPriceQuote>();
        foreach (var food in foods)
        {
            if (!byFood.TryGetValue(food.Id, out var rows)) continue;
            var quote = Quote(food, regionCode, currency, rows, sourceNames);
            if (quote is not null) quotes[food.Id] = quote;
        }
        return quotes;
    }

    /// <summary>Price per gram of one row, or null when its unit cannot be converted for this food.</summary>
    public static decimal? PerGram(FoodPrice price, Food food)
    {
        if (price.Price < 0) return null;
        var grams = PriceUnits.Grams(price.Quantity, price.Unit, PriceUnits.Normalize(price.Unit) == PriceUnits.Piece ? food.ServingSize : null);
        return grams is > 0 ? price.Price / grams.Value : null;
    }
}
