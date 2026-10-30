using System.Globalization;
using Mizan.Domain.Pricing;

namespace Mizan.Domain.Planning;

/// <summary>
/// The budget repair code makes before asking the model again (docs/PLANNING.md#ai-planning).
/// The costliest lines go first. Each is swapped for the cheapest offered item of the same role
/// at the same calories, or, failing that, has its portion cut, and only while the day still meets
/// the targets it met before. It stops once the plan is within its budget. Pure: the caller
/// passes the priced lines, the day totals and the priced options, and checks the result again.
/// </summary>
public static class BudgetRepair
{
    public enum ItemKind { Food, Recipe }

    /// <summary>One priced line of a draft. <c>Amount</c> is grams of a food or servings of a recipe.</summary>
    public sealed record Line(int Index, int DayIndex, ItemKind Kind, Guid ItemId, string Name, decimal Amount, EntryNutrition Nutrition, decimal Cost);

    /// <summary>
    /// An item a line may become, fully priced. <c>PerUnit</c> and <c>CostPerUnit</c> are per gram
    /// of a food and per serving of a recipe.
    /// </summary>
    public sealed record Option(ItemKind Kind, Guid ItemId, string Name, EntryNutrition PerUnit, decimal CostPerUnit);

    /// <summary>A line rewritten: the same line now holds this item in this amount.</summary>
    public sealed record Change(int Index, ItemKind Kind, Guid ItemId, string Name, decimal Amount, string ReplacedName, decimal Saving);

    /// <summary>The daily targets the draft is held to (<see cref="MacroTargets"/>).</summary>
    public sealed record Targets(decimal? Calories, decimal? Protein, decimal? MaxCarbs);

    /// <summary>Portions are never cut below half, as <see cref="PortionScaling"/> scales them.</summary>
    public const decimal Floor = PortionScaling.Floor;

    /// <param name="planCost">The cost of the whole plan as drafted.</param>
    /// <param name="planBudget">The budget for the plan's length: the weekly budget over seven days times the days.</param>
    public static IReadOnlyList<Change> Repair(
        IReadOnlyList<Line> lines,
        IReadOnlyDictionary<int, EntryNutrition> dayTotals,
        IReadOnlyList<Option> options,
        Targets targets,
        decimal planCost,
        decimal planBudget)
    {
        var over = planCost - planBudget;
        var changes = new List<Change>();
        if (over <= 0) return changes;

        var days = dayTotals.ToDictionary(d => d.Key, d => d.Value);
        foreach (var line in lines.Where(l => l.Cost > 0).OrderByDescending(l => l.Cost).ThenBy(l => l.DayIndex).ThenBy(l => l.Index))
        {
            if (over <= 0) break;
            var before = days.GetValueOrDefault(line.DayIndex, EntryNutrition.Zero);
            var change = Swap(line, before, options, targets) ?? Cut(line, before, targets, over);
            if (change is null) continue;

            var nutrition = NutritionOf(change, line, options);
            days[line.DayIndex] = Subtract(before, line.Nutrition).Add(nutrition);
            over -= change.Saving;
            changes.Add(change);
        }
        return changes;
    }

    /// <summary>
    /// The least a day can cost and still reach the protein and calorie targets with these options:
    /// each target bought at the cheapest price per gram of protein, or per kcal, offered. A plan
    /// that meets the targets cannot cost less than this a day, though it may need to cost more.
    /// Null without a target or a priced option that supplies it.
    /// </summary>
    public static (decimal DailyCost, string Basis)? LowerBound(IReadOnlyList<Option> options, Targets targets)
    {
        (decimal, string)? best = null;
        if (targets.Protein is { } protein && protein > 0)
        {
            var perGram = options.Where(o => o.PerUnit.ProteinGrams > 0).Select(o => o.CostPerUnit / o.PerUnit.ProteinGrams).DefaultIfEmpty(-1).Min();
            if (perGram >= 0) best = (protein * perGram, string.Create(CultureInfo.InvariantCulture, $"the {protein:0} g protein target"));
        }
        if (targets.Calories is { } kcal && kcal > 0)
        {
            var perKcal = options.Where(o => o.PerUnit.Calories > 0).Select(o => o.CostPerUnit / o.PerUnit.Calories).DefaultIfEmpty(-1).Min();
            // The check allows a tenth under the target, so the bound does too.
            var floor = kcal * (1 - PlanRuleCheck.Tolerance);
            if (perKcal >= 0 && (best is null || floor * perKcal > best.Value.Item1))
                best = (floor * perKcal, string.Create(CultureInfo.InvariantCulture, $"the {kcal:0} kcal target"));
        }
        return best;
    }

    /// <summary>
    /// The cheapest option of the same role at the line's calories that saves money and keeps the
    /// day's targets. A recipe takes the place of a recipe; a food takes the place of a food whose
    /// calories come mostly from the same macronutrient.
    /// </summary>
    private static Change? Swap(Line line, EntryNutrition day, IReadOnlyList<Option> options, Targets targets)
    {
        if (line.Nutrition.Calories <= 0) return null;
        var role = Role(line.Nutrition);
        return options
            .Where(o => o.Kind == line.Kind && o.ItemId != line.ItemId && o.PerUnit.Calories > 0)
            .Where(o => line.Kind == ItemKind.Recipe || Role(o.PerUnit) == role)
            .Select(o =>
            {
                var amount = Round(o.Kind, line.Nutrition.Calories / o.PerUnit.Calories, up: false);
                return (Option: o, Amount: amount, Nutrition: Times(o.PerUnit, amount), Cost: o.CostPerUnit * amount);
            })
            .Where(c => c.Amount > 0 && c.Cost < line.Cost)
            .Where(c => Keeps(day, Subtract(day, line.Nutrition).Add(c.Nutrition), targets))
            .OrderBy(c => c.Cost)
            .ThenBy(c => c.Option.Name, StringComparer.Ordinal)
            .Select(c => new Change(line.Index, c.Option.Kind, c.Option.ItemId, c.Option.Name, c.Amount, line.Name, line.Cost - c.Cost))
            .FirstOrDefault();
    }

    /// <summary>
    /// The line's portion cut by what the budget still needs, never below half, and only on a plan
    /// with a calorie target, while the day keeps its calories and protein within the tolerance of
    /// their targets. Without a calorie target code cannot tell how much less is still enough.
    /// </summary>
    private static Change? Cut(Line line, EntryNutrition day, Targets targets, decimal over)
    {
        if (targets.Calories is not { } kcal || kcal <= 0 || line.Amount <= 0) return null;

        var share = Math.Min(1 - Floor, over / line.Cost);
        if (line.Nutrition.Calories > 0)
            share = Math.Min(share, (day.Calories - kcal * (1 - PlanRuleCheck.Tolerance)) / line.Nutrition.Calories);
        if (targets.Protein is { } protein && protein > 0 && line.Nutrition.ProteinGrams > 0)
            share = Math.Min(share, (day.ProteinGrams - protein * (1 - PlanRuleCheck.Tolerance)) / line.Nutrition.ProteinGrams);
        if (share <= 0) return null;

        // Rounded up, so the cut never takes the day past the limits just worked out.
        var amount = Math.Max(Round(line.Kind, line.Amount * (1 - share), up: true), Round(line.Kind, line.Amount * Floor, up: true));
        if (amount >= line.Amount) return null;
        var saving = line.Cost * (1 - amount / line.Amount);
        return new Change(line.Index, line.Kind, line.ItemId, line.Name, amount, line.Name, saving);
    }

    /// <summary>Whether the day after a change still meets each target it met before.</summary>
    private static bool Keeps(EntryNutrition before, EntryNutrition after, Targets targets)
    {
        var tolerance = PlanRuleCheck.Tolerance;
        if (targets.Calories is { } kcal && kcal > 0)
        {
            if (after.Calories > kcal * (1 + tolerance) && after.Calories > before.Calories) return false;
            if (after.Calories < kcal * (1 - tolerance) && after.Calories < before.Calories) return false;
        }
        if (targets.Protein is { } protein && protein > 0
            && after.ProteinGrams < protein * (1 - tolerance) && after.ProteinGrams < before.ProteinGrams) return false;
        if (targets.MaxCarbs is { } carbs && after.CarbsGrams > carbs && after.CarbsGrams > before.CarbsGrams) return false;
        return true;
    }

    private enum Macro { Protein, Carbs, Fat }

    /// <summary>The macronutrient most of the calories come from.</summary>
    private static Macro Role(EntryNutrition n)
    {
        var protein = n.ProteinGrams * 4;
        var carbs = n.CarbsGrams * 4;
        var fat = n.FatGrams * 9;
        return protein >= carbs && protein >= fat ? Macro.Protein : carbs >= fat ? Macro.Carbs : Macro.Fat;
    }

    /// <summary>Grams to 5 g between 5 g and 2 kg; servings to a quarter between a quarter and 20, as a draft's entries are capped.</summary>
    private static decimal Round(ItemKind kind, decimal amount, bool up)
    {
        var (step, min, max) = kind == ItemKind.Food ? (5m, 5m, 2000m) : (0.25m, 0.25m, 20m);
        var steps = amount / step;
        var rounded = (up ? Math.Ceiling(steps) : Math.Round(steps, MidpointRounding.AwayFromZero)) * step;
        return Math.Clamp(rounded, min, max);
    }

    private static EntryNutrition NutritionOf(Change change, Line line, IReadOnlyList<Option> options) =>
        change.ItemId == line.ItemId && change.Kind == line.Kind
            ? Times(line.Nutrition, change.Amount / line.Amount)
            : Times(options.First(o => o.Kind == change.Kind && o.ItemId == change.ItemId).PerUnit, change.Amount);

    private static EntryNutrition Times(EntryNutrition n, decimal factor) =>
        new(n.Calories * factor, n.ProteinGrams * factor, n.CarbsGrams * factor, n.FatGrams * factor, n.FiberGrams * factor);

    private static EntryNutrition Subtract(EntryNutrition a, EntryNutrition b) =>
        new(a.Calories - b.Calories, a.ProteinGrams - b.ProteinGrams, a.CarbsGrams - b.CarbsGrams, a.FatGrams - b.FatGrams, a.FiberGrams - b.FiberGrams);

    /// <summary>The lines that cost most, for the unmet line: "Most of the cost: Tilapia (ETB 1,208.20 a week), ...".</summary>
    public static string? CostDrivers(IEnumerable<(string Name, decimal WeeklyCost)> items, string? currency, int count = 2)
    {
        var top = items.Where(i => i.WeeklyCost > 0).OrderByDescending(i => i.WeeklyCost).ThenBy(i => i.Name, StringComparer.Ordinal).Take(count).ToList();
        return top.Count == 0
            ? null
            : "Most of the cost: " + string.Join(", ", top.Select(i => $"{i.Name} ({Money.Format(i.WeeklyCost, currency)} a week)")) + ".";
    }
}
