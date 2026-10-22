using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Application.Prices;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Planning;

/// <summary>
/// The cost table of a generated draft: the plan becomes shopping lines through
/// <see cref="ShoppingAggregator"/>, <see cref="PriceBookReader"/> quotes them in the
/// person's region, and <see cref="PlanCostCalculator"/> adds them up. It is the same
/// path the plan page and the shopping list take, line by line, so the numbers agree.
/// </summary>
public sealed class PlanCostTable
{
    private readonly IMizanDbContext _context;
    private readonly PriceBookReader _prices;

    public PlanCostTable(IMizanDbContext context, PriceBookReader prices)
    {
        _context = context;
        _prices = prices;
    }

    public async Task<PlanCostDto> CostAsync(MealPlan plan, Guid userId, CancellationToken cancellationToken)
    {
        var region = await _prices.RegionForAsync(userId, cancellationToken);

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
        var quotes = await _prices.QuotesAsync(foods.Values.ToList(), region, cancellationToken);
        var summary = PlanCostCalculator.Cost(lines, quotes, region.Currency);
        return new PlanCostDto(
            region.Code,
            region.Currency,
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
