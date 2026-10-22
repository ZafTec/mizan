using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Planning;

/// <summary>
/// Prices a plan in the person's region: the plan becomes shopping lines through
/// <see cref="ShoppingAggregator"/>, each line gets the price <see cref="PriceBook"/> picks, and
/// <see cref="PlanCostCalculator"/> adds them up. Loading is all this does; the rules are Domain's.
/// </summary>
public sealed class PlanCosting
{
    /// <summary>The region a person without one is priced in (docs/PLANNING.md#prices).</summary>
    public const string DefaultRegion = "ET-AA";

    private const string DefaultCurrency = "ETB";

    private readonly IMizanDbContext _context;

    public PlanCosting(IMizanDbContext context) => _context = context;

    /// <summary>The person's price region and its currency.</summary>
    public async Task<(string Region, string Currency)> RegionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var region = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PriceRegionCode)
            .FirstOrDefaultAsync(cancellationToken) ?? DefaultRegion;
        var currency = await _context.PriceRegions.AsNoTracking()
            .Where(r => r.Code == region)
            .Select(r => r.Currency)
            .FirstOrDefaultAsync(cancellationToken) ?? DefaultCurrency;
        return (region, currency);
    }

    public async Task<PlanCostDto> CostAsync(MealPlan plan, Guid userId, CancellationToken cancellationToken)
    {
        var (region, currency) = await RegionAsync(userId, cancellationToken);

        var recipeIds = plan.Entries.Where(e => e.RecipeId.HasValue).Select(e => e.RecipeId!.Value).Distinct().ToList();
        var recipes = recipeIds.Count == 0
            ? new Dictionary<Guid, Recipe>()
            : await _context.Recipes.AsNoTracking().Include(r => r.Ingredients)
                .Where(r => recipeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        var foodIds = plan.Entries.Where(e => e.FoodId.HasValue).Select(e => e.FoodId!.Value)
            .Concat(recipes.Values.SelectMany(r => r.Ingredients).Where(i => i.FoodId.HasValue).Select(i => i.FoodId!.Value))
            .Distinct().ToList();
        var foods = foodIds.Count == 0
            ? new Dictionary<Guid, Food>()
            : await _context.Foods.AsNoTracking().Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, cancellationToken);

        var lines = ShoppingAggregator.Aggregate(plan.Entries, recipes, foods);
        var pricedIds = lines.Where(l => l.FoodId.HasValue).Select(l => l.FoodId!.Value).Distinct().ToList();
        var prices = pricedIds.Count == 0
            ? []
            : await _context.FoodPrices.AsNoTracking()
                .Where(p => pricedIds.Contains(p.FoodId) && p.RegionCode == region)
                .ToListAsync(cancellationToken);
        var quotes = PriceBook.QuoteAll(pricedIds.Where(foods.ContainsKey).Select(id => foods[id]), region, currency, prices);

        var summary = PlanCostCalculator.Cost(lines, quotes, currency);
        return new PlanCostDto(
            region,
            currency,
            summary.Total,
            PlanCostCalculator.PerWeek(summary.Total, plan.DurationDays),
            plan.Rules.WeeklyBudget,
            summary.PricedItems,
            summary.TotalItems,
            summary.Coverage,
            summary.LowestConfidence?.ToString(),
            summary.Items
                .Select(i => new PlanCostItemDto(
                    i.Name,
                    i.FoodId,
                    i.Grams is { } g ? Math.Round(g, 1) : null,
                    i.Cost,
                    i.Confidence?.ToString(),
                    i.Reason))
                .ToList());
    }

    /// <summary>
    /// The weekly figure the budget rule compares, or null when the rules count money in another
    /// currency: a budget in dollars is not checked against a total in birr.
    /// </summary>
    public static decimal? ComparableWeeklyCost(PlanRules rules, PlanCostDto cost) =>
        rules.Currency is null || string.Equals(rules.Currency, cost.Currency, StringComparison.OrdinalIgnoreCase)
            ? cost.WeeklyTotal
            : null;
}

/// <summary>The stored generation metadata as it crosses the wire.</summary>
public static class PlanGenerationMapping
{
    public static PlanGenerationDto? ToDto(PlanGeneration? generation) =>
        generation is null
            ? null
            : new PlanGenerationDto(
                generation.PromptKey,
                generation.PromptVersionId,
                generation.PromptVersion,
                generation.Model,
                generation.BudgetCeiling,
                generation.Currency,
                generation.GeneratedAt);
}
