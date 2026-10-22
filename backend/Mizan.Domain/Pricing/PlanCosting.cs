using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Domain.Pricing;

/// <summary>What a plan costs as a whole, per day, and per entry. A day or an entry with nothing priced has no cost, not a zero one.</summary>
public sealed record PlanCost(
    CostSummary Summary,
    decimal? TotalCost,
    decimal? WeeklyCost,
    IReadOnlyDictionary<int, decimal?> DayCosts,
    IReadOnlyDictionary<Guid, decimal?> EntryCosts);

/// <summary>
/// Costs a plan the way its shopping list would: entries are aggregated into
/// lines with <see cref="ShoppingAggregator"/>, and the lines are priced with
/// <see cref="PlanCostCalculator"/>. The whole plan, each day and each entry
/// go through the same two steps, so the numbers on every surface agree.
/// </summary>
public static class PlanCosting
{
    public static PlanCost Cost(
        IReadOnlyCollection<PlanEntry> entries,
        int durationDays,
        IReadOnlyDictionary<Guid, Recipe> recipesById,
        IReadOnlyDictionary<Guid, Food> foodsById,
        IReadOnlyDictionary<Guid, FoodPriceQuote> quotes,
        string currency)
    {
        CostSummary Of(IEnumerable<PlanEntry> some) =>
            PlanCostCalculator.Cost(ShoppingAggregator.Aggregate(some, recipesById, foodsById), quotes, currency);

        var summary = Of(entries);
        var days = entries.GroupBy(e => e.DayIndex).ToDictionary(g => g.Key, g => PricedTotal(Of(g)));
        var perEntry = entries.ToDictionary(e => e.Id, e => PricedTotal(Of([e])));
        var total = PricedTotal(summary);
        var weekly = total is { } t ? PlanCostCalculator.PerWeek(t, durationDays) : (decimal?)null;
        return new PlanCost(summary, total, weekly, days, perEntry);
    }

    /// <summary>The summary's total, or null when not one line in it is priced.</summary>
    public static decimal? PricedTotal(CostSummary summary) => summary.PricedItems == 0 ? null : summary.Total;

    /// <summary>
    /// Whether a weekly cost is over the plan's budget. Null when there is no
    /// budget, no cost, or the budget is in another currency: amounts in two
    /// currencies are not compared.
    /// </summary>
    public static bool? OverBudget(decimal? weeklyCost, decimal? weeklyBudget, string? budgetCurrency, string currency)
    {
        if (weeklyCost is not { } cost || weeklyBudget is not { } budget) return null;
        if (!string.Equals(budgetCurrency, currency, StringComparison.OrdinalIgnoreCase)) return null;
        return cost > budget;
    }
}
