using Mizan.Domain.Entities;
using Mizan.Domain.Pricing;

namespace Mizan.Domain.Planning;

/// <summary>One way a plan breaks its own rules. Shown the same on every surface.</summary>
public sealed record PlanViolation(int? DayIndex, string? Slot, string Rule, string Message);

/// <summary>Rule names, stable for clients and tests.</summary>
public static class PlanRuleNames
{
    public const string ForbiddenFood = "forbidden_food";
    public const string NotAllowedFood = "not_allowed_food";
    public const string CaloriesOver = "calories_over";
    public const string ProteinUnder = "protein_under";
    public const string CarbsOver = "carbs_over";
    public const string BudgetOver = "budget_over";
    public const string DailyBudgetOver = "daily_budget_over";
    public const string Unresolved = "unresolved";
}

/// <summary>
/// Evaluates a plan against its rules. Pure: the caller supplies the day
/// totals and the foods each entry reaches (its own food, or its recipe's
/// ingredient foods). A day over its calorie target by more than a tenth, or
/// under its protein target by more than a tenth, is a violation; a carb
/// ceiling and a budget are absolute. The caller passes only costs in the
/// rules' currency: an amount in another currency is not compared.
/// </summary>
public static class PlanRuleCheck
{
    public const decimal Tolerance = 0.10m;

    public static IReadOnlyList<PlanViolation> Evaluate(
        PlanRules rules,
        IReadOnlyList<PlanEntry> entries,
        IReadOnlyList<PlanDayNutrition> days,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> foodIdsByEntry,
        IReadOnlyDictionary<Guid, string> foodNames,
        decimal? weeklyCost = null,
        IReadOnlyDictionary<int, decimal?>? dayCosts = null)
    {
        var violations = new List<PlanViolation>();
        var forbidden = rules.ForbiddenFoodIds.ToHashSet();
        var allowed = rules.AllowedFoodIds.ToHashSet();

        foreach (var entry in entries.OrderBy(e => e.DayIndex).ThenBy(e => PlanSlots.Order(e.Slot)).ThenBy(e => e.SortOrder))
        {
            if (!foodIdsByEntry.TryGetValue(entry.Id, out var foods)) continue;
            foreach (var foodId in foods.Distinct())
            {
                var name = foodNames.TryGetValue(foodId, out var n) ? n : "a food";
                if (forbidden.Contains(foodId))
                    violations.Add(new PlanViolation(entry.DayIndex, entry.Slot, PlanRuleNames.ForbiddenFood, $"{name} is on the forbidden list."));
                else if (allowed.Count > 0 && !allowed.Contains(foodId))
                    violations.Add(new PlanViolation(entry.DayIndex, entry.Slot, PlanRuleNames.NotAllowedFood, $"{name} is not on the allowed list."));
            }
        }

        var maxCarbs = rules.EffectiveMaxCarbs;
        foreach (var day in days)
        {
            if (day.UnresolvedEntryIds.Count > 0)
                violations.Add(new PlanViolation(day.DayIndex, null, PlanRuleNames.Unresolved,
                    $"{day.UnresolvedEntryIds.Count} {(day.UnresolvedEntryIds.Count == 1 ? "entry has" : "entries have")} no nutrition yet, so the day's totals are incomplete."));

            if (rules.TargetCalories is { } kcal && day.Totals.Calories > kcal * (1 + Tolerance))
                violations.Add(new PlanViolation(day.DayIndex, null, PlanRuleNames.CaloriesOver,
                    $"{day.Totals.Calories:0} kcal is over the {kcal:0} kcal target."));

            if (rules.TargetProtein is { } protein && protein > 0 && day.Totals.ProteinGrams < protein * (1 - Tolerance) && day.UnresolvedEntryIds.Count == 0)
                violations.Add(new PlanViolation(day.DayIndex, null, PlanRuleNames.ProteinUnder,
                    $"{day.Totals.ProteinGrams:0} g protein is under the {protein:0} g target."));

            if (maxCarbs is { } carbs && day.Totals.CarbsGrams > carbs)
                violations.Add(new PlanViolation(day.DayIndex, null, PlanRuleNames.CarbsOver,
                    $"{day.Totals.CarbsGrams:0} g carbs is over the {carbs:0} g ceiling."));

            if (OverBudget(dayCosts?.GetValueOrDefault(day.DayIndex), rules.DailyBudget) is { } dayCost)
                violations.Add(new PlanViolation(day.DayIndex, null, PlanRuleNames.DailyBudgetOver,
                    $"{Money.Format(dayCost, rules.Currency)} this day is over the {Money.Format(rules.DailyBudget!.Value, rules.Currency)} daily budget."));
        }

        if (OverBudget(weeklyCost, rules.WeeklyBudget) is { } cost)
            violations.Add(new PlanViolation(null, null, PlanRuleNames.BudgetOver,
                $"{Money.Format(cost, rules.Currency)} a week is over the {Money.Format(rules.WeeklyBudget!.Value, rules.Currency)} budget."));

        return violations;
    }

    /// <summary>The cost when it is over the budget, else null.</summary>
    private static decimal? OverBudget(decimal? cost, decimal? budget) =>
        cost is { } c && budget is { } b && c > b ? c : null;
}
