using Mizan.Domain.Planning;

namespace Mizan.Domain.Pricing;

/// <summary>The cost of one shopping line, or why it has none.</summary>
public sealed record ItemCost(
    string Name,
    Guid? FoodId,
    decimal? Grams,
    decimal? Cost,
    PriceConfidence? Confidence,
    string? Reason);

/// <summary>What a plan or a list costs, with how much of it is actually priced.</summary>
public sealed record CostSummary(
    string Currency,
    decimal Total,
    int PricedItems,
    int TotalItems,
    PriceConfidence? LowestConfidence,
    IReadOnlyList<ItemCost> Items)
{
    /// <summary>Share of lines that carry a price, 0 to 1.</summary>
    public decimal Coverage => TotalItems == 0 ? 0m : Math.Round((decimal)PricedItems / TotalItems, 2);

    public IEnumerable<ItemCost> Unpriced => Items.Where(i => i.Cost is null);
}

/// <summary>
/// Multiplies aggregated grams by the price per gram. Pure, and never guesses:
/// a line without a weight or without a quote is listed as unpriced, and the
/// total says how many lines it covers.
/// </summary>
public static class PlanCostCalculator
{
    public static CostSummary Cost(
        IEnumerable<ShoppingDraftItem> items,
        IReadOnlyDictionary<Guid, FoodPriceQuote> quotes,
        string currency)
    {
        var costs = new List<ItemCost>();
        decimal total = 0;
        var priced = 0;
        PriceConfidence? lowest = null;

        foreach (var item in items)
        {
            if (item.FoodId is not { } foodId)
            {
                costs.Add(new ItemCost(item.Name, null, item.Grams, null, null, "No catalogue food is linked."));
                continue;
            }
            if (item.Grams is not { } grams || grams <= 0)
            {
                costs.Add(new ItemCost(item.Name, foodId, null, null, null, "The amount is not known in grams."));
                continue;
            }
            if (!quotes.TryGetValue(foodId, out var quote))
            {
                costs.Add(new ItemCost(item.Name, foodId, grams, null, null, "No price for this region yet."));
                continue;
            }

            var cost = Math.Round(grams * quote.PricePerGram, 2);
            total += cost;
            priced++;
            lowest = lowest is null || quote.Confidence < lowest ? quote.Confidence : lowest;
            costs.Add(new ItemCost(item.Name, foodId, grams, cost, quote.Confidence, null));
        }

        return new CostSummary(currency, Math.Round(total, 2), priced, costs.Count, lowest, costs);
    }

    /// <summary>A weekly figure from a plan of any length, so budgets compare like with like.</summary>
    public static decimal PerWeek(decimal total, int durationDays) =>
        durationDays <= 0 ? total : Math.Round(total / durationDays * 7m, 2);
}
