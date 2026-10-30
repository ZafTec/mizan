using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Application.Prices;
using Mizan.Domain.Ai;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Planning;

/// <summary>What the model is told for one plan, and what code needs to check its answer against.</summary>
public sealed record MealPlanContext(
    string Text,
    IReadOnlyDictionary<Guid, string> Recipes,
    IReadOnlyList<Food> Foods,
    string Region,
    string Currency,
    IReadOnlyList<string> IncludedAxes,
    GoalTargets? Goal = null)
{
    /// <summary>
    /// What code may put in a line's place to bring the cost down: the offered foods with a price,
    /// per gram, and the recipes whose every ingredient is priced, per serving.
    /// </summary>
    public IReadOnlyList<BudgetRepair.Option> Options { get; init; } = [];
}

/// <summary>
/// Builds the context of a plan generation. What is personal comes through
/// <see cref="IDataAccessPolicy"/> and is absent when withheld: the goal needs the nutrition axis,
/// the person's recipes and own foods need the planning axis. The public catalogue and the price
/// book are not personal and are always offered, narrowed to the allow list, the foods the
/// preference names, and the foods that have a price (docs/AI.md#consent).
/// </summary>
public sealed class MealPlanContextBuilder
{
    private const int MaxRecipes = 30;
    private const int MaxFoods = 80;
    private const int FoodsPerWord = 5;
    private const int PricedFoods = 40;

    private readonly IMizanDbContext _context;
    private readonly IDataAccessPolicy _policy;
    private readonly IHouseholdAccess _households;
    private readonly PriceBookReader _prices;

    public MealPlanContextBuilder(IMizanDbContext context, IDataAccessPolicy policy, IHouseholdAccess households, PriceBookReader prices)
    {
        _context = context;
        _policy = policy;
        _households = households;
        _prices = prices;
    }

    /// <param name="includeGoal">False when the caller already states the goal, as meal suggestions do.</param>
    public async Task<MealPlanContext> BuildAsync(
        Guid userId, PlanRules rules, string? preference, CancellationToken cancellationToken, bool includeGoal = true)
    {
        var axes = await _policy.ReadableAxesAsync(userId, userId, AccessPurpose.AiContext, cancellationToken);
        var region = await _prices.RegionForAsync(userId, cancellationToken);
        var currency = region.Currency;
        var text = new StringBuilder();
        var included = new List<string>();
        GoalTargets? goalTargets = null;

        if (axes.Contains(DataAxis.Nutrition))
        {
            var goal = await _context.UserGoals.AsNoTracking()
                .Where(g => g.UserId == userId && g.IsActive)
                .OrderByDescending(g => g.UpdatedAt)
                .Select(g => new { g.TargetCalories, g.TargetProteinGrams, g.TargetCarbsGrams, g.TargetFatGrams })
                .FirstOrDefaultAsync(cancellationToken);
            if (goal is not null && includeGoal)
            {
                goalTargets = new GoalTargets(goal.TargetCalories, goal.TargetProteinGrams, goal.TargetCarbsGrams, goal.TargetFatGrams);
                text.Append("## Goal\n").Append(CultureInfo.InvariantCulture,
                    $"Daily targets: {Num(goal.TargetCalories)} kcal, protein {Num(goal.TargetProteinGrams)} g, carbs {Num(goal.TargetCarbsGrams)} g, fat {Num(goal.TargetFatGrams)} g.\n\n");
            }
            included.Add("nutrition");
        }

        var personal = axes.Contains(DataAxis.Planning);
        var recipes = new Dictionary<Guid, string>();
        var options = new List<BudgetRepair.Option>();
        if (personal)
        {
            included.Add("planning");
            var households = await _households.AccessibleIdsAsync(cancellationToken);
            var rows = await _context.Recipes.AsNoTracking().Include(r => r.Ingredients)
                .Where(r => r.UserId == userId || (r.HouseholdId != null && households.Contains(r.HouseholdId.Value)))
                .OrderByDescending(r => r.UpdatedAt)
                .Take(MaxRecipes)
                .ToListAsync(cancellationToken);
            if (rows.Count > 0)
            {
                var perServing = await RecipeNutritionLookup.ForRecipesAsync(_context, rows.Select(r => r.Id).ToList(), cancellationToken);
                var servingCosts = await ServingCostsAsync(rows, region, cancellationToken);
                text.Append(CultureInfo.InvariantCulture, $"## Your recipes (recipeId | title | serves | per serving | cost per serving in {currency})\n");
                foreach (var row in rows)
                {
                    recipes[row.Id] = row.Title;
                    text.Append(CultureInfo.InvariantCulture, $"{row.Id} | {Clean(row.Title)} | {row.Servings} | ");
                    var known = perServing.TryGetValue(row.Id, out var n);
                    text.Append(known
                        ? string.Create(CultureInfo.InvariantCulture, $"{n.Calories:0} kcal, P {n.ProteinGrams:0} g, C {n.CarbsGrams:0} g, F {n.FatGrams:0} g")
                        : "nutrition unknown");
                    var cost = servingCosts[row.Id];
                    text.Append(" | ").Append(cost.PricedItems == 0
                        ? "no price"
                        : cost.PricedItems < cost.TotalItems
                            ? string.Create(CultureInfo.InvariantCulture, $"at least {cost.Total:0.##} ({cost.PricedItems} of {cost.TotalItems} ingredients priced)")
                            : string.Create(CultureInfo.InvariantCulture, $"{cost.Total:0.##}"));
                    text.Append('\n');
                    // Only a fully priced recipe may replace a line: a partial price would look cheaper than it is.
                    if (known && cost.PricedItems > 0 && cost.PricedItems == cost.TotalItems)
                        options.Add(new BudgetRepair.Option(BudgetRepair.ItemKind.Recipe, row.Id, row.Title,
                            new EntryNutrition(n.Calories, n.ProteinGrams, n.CarbsGrams, n.FatGrams, n.FiberGrams), cost.Total));
                }
                text.Append('\n');
            }
        }

        var foods = await FoodsAsync(userId, personal, rules, preference, region.Code, cancellationToken);
        var quotes = await _prices.QuotesAsync(foods, region, cancellationToken);

        if (foods.Count > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"## Catalogue foods (name | per 100 g | price per 100 g in {currency})\n");
            foreach (var food in foods)
            {
                text.Append(CultureInfo.InvariantCulture,
                    $"{Clean(food.Name)} | {food.CaloriesPer100g:0} kcal, P {food.ProteinPer100g:0.#} g, C {food.CarbsPer100g:0.#} g, F {food.FatPer100g:0.#} g | ");
                text.Append(quotes.TryGetValue(food.Id, out var quote)
                    ? string.Create(CultureInfo.InvariantCulture, $"{quote.PricePerGram * 100m:0.##}")
                    : "no price");
                text.Append('\n');
                if (quote is not null && quote.PricePerGram > 0)
                    options.Add(new BudgetRepair.Option(BudgetRepair.ItemKind.Food, food.Id, food.Name,
                        new EntryNutrition(food.CaloriesPer100g / 100m, food.ProteinPer100g / 100m, food.CarbsPer100g / 100m,
                            food.FatPer100g / 100m, (food.FiberPer100g ?? 0m) / 100m),
                        quote.PricePerGram));
            }
            text.Append('\n');
        }

        if (rules.ForbiddenFoodIds.Count > 0)
        {
            var forbidden = await _context.Foods.AsNoTracking()
                .Where(f => rules.ForbiddenFoodIds.Contains(f.Id))
                .Select(f => f.Name)
                .ToListAsync(cancellationToken);
            if (forbidden.Count > 0)
                text.Append("## Never use\n").Append(string.Join("\n", forbidden.Select(Clean))).Append("\n\n");
        }

        return new MealPlanContext(text.ToString(), recipes, foods, region.Code, currency, included, goalTargets) { Options = options };
    }

    /// <summary>
    /// One serving of each recipe costed as a plan entry is (<see cref="PlanCosting"/>), so the
    /// figure the model reads is the one the draft's cost table will show.
    /// </summary>
    private async Task<Dictionary<Guid, CostSummary>> ServingCostsAsync(
        IReadOnlyList<Recipe> recipes, PriceRegionInfo region, CancellationToken cancellationToken)
    {
        var foodIds = recipes.SelectMany(r => r.Ingredients).Where(i => i.FoodId != null).Select(i => i.FoodId!.Value).Distinct().ToList();
        var foods = foodIds.Count == 0
            ? new Dictionary<Guid, Food>()
            : await _context.Foods.AsNoTracking().Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, cancellationToken);
        var quotes = await _prices.QuotesAsync(foods.Values, region, cancellationToken);
        var byId = recipes.ToDictionary(r => r.Id);
        return recipes.ToDictionary(r => r.Id, r => PlanCosting.Cost(
            [new PlanEntry { Id = Guid.NewGuid(), Kind = PlanEntryKind.Recipe, RecipeId = r.Id, Servings = 1m }],
            1, byId, foods, quotes, region.Currency).Summary);
    }

    /// <summary>
    /// The allow list when there is one; otherwise foods the preference names and foods with a
    /// price in the region. Forbidden foods are never offered. Own foods only on the planning axis.
    /// </summary>
    private async Task<List<Food>> FoodsAsync(
        Guid userId, bool personal, PlanRules rules, string? preference, string region, CancellationToken cancellationToken)
    {
        var usable = _context.Foods.AsNoTracking()
            .Where(f => f.UserId == null || (personal && f.UserId == userId))
            .Where(f => !rules.ForbiddenFoodIds.Contains(f.Id));

        if (rules.AllowedFoodIds.Count > 0)
        {
            return await usable.Where(f => rules.AllowedFoodIds.Contains(f.Id))
                .OrderBy(f => f.Name).Take(MaxFoods).ToListAsync(cancellationToken);
        }

        var found = new Dictionary<Guid, Food>();
        foreach (var word in Words(preference))
        {
            var matches = await usable.Where(f => f.Name.ToLower().Contains(word))
                .OrderByDescending(f => f.IsVerified).ThenBy(f => f.Name.Length)
                .Take(FoodsPerWord).ToListAsync(cancellationToken);
            foreach (var food in matches) found.TryAdd(food.Id, food);
        }

        var pricedIds = await _context.FoodPrices.AsNoTracking()
            .Where(p => p.RegionCode == region)
            .GroupBy(p => p.FoodId)
            .OrderByDescending(g => g.Max(p => p.ObservedAt))
            .Select(g => g.Key)
            .Take(PricedFoods)
            .ToListAsync(cancellationToken);
        if (pricedIds.Count > 0)
        {
            foreach (var food in await usable.Where(f => pricedIds.Contains(f.Id)).ToListAsync(cancellationToken))
                found.TryAdd(food.Id, food);
        }

        return found.Values.OrderBy(f => f.Name).Take(MaxFoods).ToList();
    }

    /// <summary>The words of a preference worth searching the catalogue for.</summary>
    internal static IEnumerable<string> Words(string? preference) =>
        FoodNameMatch.Normalize(preference)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsLetter).ToArray()))
            .Where(w => w.Length >= 4)
            .Distinct()
            .Take(8);

    private static string Num(decimal? value) => value is { } v ? v.ToString("0", CultureInfo.InvariantCulture) : "not set";

    /// <summary>One line per item: a name with a line break or a bar in it cannot forge another row.</summary>
    private static string Clean(string value) => value.Replace('\n', ' ').Replace('\r', ' ').Replace('|', '/').Trim();
}
