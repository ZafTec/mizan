using System.Globalization;
using Mizan.Domain.Pricing;

namespace Mizan.Domain.Planning;

/// <summary>
/// What a generated draft is told about itself, written by code from the checks, never by the
/// model. Each rule the draft does not meet is one line naming the amount and the target, and
/// the note says how many there are and what code changed.
/// </summary>
public static class DraftSummary
{
    public sealed record Summary(string Note, IReadOnlyList<string> Unmet);

    /// <param name="violations">The checks' findings on the draft as saved.</param>
    /// <param name="weeklyCost">The draft's cost for a week, in <paramref name="currency"/>.</param>
    /// <param name="weeklyBudget">The weekly ceiling, in <paramref name="currency"/>.</param>
    /// <param name="unresolvedLines">Lines saved as text because the catalogue does not have them.</param>
    /// <param name="scaledDays">Days whose portions code scaled to the calorie target.</param>
    /// <param name="budgetReason">Why the budget is not met, from <see cref="BudgetReason"/>; added to the budget line.</param>
    /// <param name="budgetChanges">Lines code swapped or cut to bring the cost down.</param>
    public static Summary Describe(
        IReadOnlyList<PlanViolation> violations,
        decimal? weeklyCost,
        decimal? weeklyBudget,
        string? currency,
        int unresolvedLines,
        IReadOnlyCollection<int> scaledDays,
        string? budgetReason = null,
        int budgetChanges = 0)
    {
        var unmet = new List<string>();
        foreach (var violation in violations)
        {
            var line = violation.Rule == PlanRuleNames.BudgetOver && weeklyCost is { } cost && weeklyBudget is { } budget
                ? $"Budget not met: {Money.Format(cost, currency)} a week against {Money.Format(budget, currency)}."
                    + (string.IsNullOrWhiteSpace(budgetReason) ? "" : " " + budgetReason)
                : violation.DayIndex is { } day
                    ? string.Create(CultureInfo.InvariantCulture, $"Day {day + 1}{(violation.Slot is null ? "" : $" {violation.Slot.ToLowerInvariant()}")}: {violation.Message}")
                    : violation.Message;
            if (!unmet.Contains(line)) unmet.Add(line);
        }

        var parts = new List<string>
        {
            unmet.Count == 0
                ? "Meets every rule of the plan."
                : string.Create(CultureInfo.InvariantCulture, $"{unmet.Count} {(unmet.Count == 1 ? "rule is" : "rules are")} not met; the draft is saved for review."),
        };
        if (scaledDays.Count > 0)
        {
            var days = string.Join(", ", scaledDays.Order().Select(d => (d + 1).ToString(CultureInfo.InvariantCulture)));
            parts.Add(scaledDays.Count == 1
                ? $"Portions on day {days} were scaled to the calorie target."
                : $"Portions on days {days} were scaled to the calorie target.");
        }
        if (budgetChanges > 0)
        {
            parts.Add(budgetChanges == 1
                ? "1 costly line was swapped or cut to lower the cost."
                : string.Create(CultureInfo.InvariantCulture, $"{budgetChanges} costly lines were swapped or cut to lower the cost."));
        }
        if (unresolvedLines > 0)
        {
            parts.Add(unresolvedLines == 1
                ? "1 line is not in the catalogue and is saved as text to review."
                : string.Create(CultureInfo.InvariantCulture, $"{unresolvedLines} lines are not in the catalogue and are saved as text to review."));
        }
        return new Summary(string.Join(" ", parts), unmet);
    }

    /// <summary>
    /// Why a draft is over its budget, in one or two sentences: when even the cheapest way to meet
    /// the targets with what was offered costs more than the budget, that floor; and the lines that
    /// cost most. Null when there is nothing to say.
    /// </summary>
    /// <param name="lowerBoundWeekly">The weekly floor from <see cref="BudgetRepair.LowerBound"/>, times seven.</param>
    public static string? BudgetReason(decimal? lowerBoundWeekly, string? basis, decimal? weeklyBudget, string? drivers, string? currency)
    {
        var parts = new List<string>();
        if (lowerBoundWeekly is { } floor && weeklyBudget is { } budget && floor > budget && !string.IsNullOrWhiteSpace(basis))
            parts.Add($"Meeting {basis} with the priced foods and your recipes costs at least {Money.Format(floor, currency)} a week.");
        if (!string.IsNullOrWhiteSpace(drivers)) parts.Add(drivers);
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }
}

/// <summary>
/// The repair code makes before asking the model again: a day over its calorie target has every
/// portion scaled by the same factor, so its foods and their proportions stay as written.
/// </summary>
public static class PortionScaling
{
    /// <summary>Portions are never cut below half: a day that needs more is the model's to rewrite.</summary>
    public const decimal Floor = 0.5m;

    /// <summary>
    /// The factor that brings a day's calories down to the target, or 1 when the day is within the
    /// tolerance <see cref="PlanRuleCheck"/> allows or has no target.
    /// </summary>
    public static decimal DayFactor(decimal dayCalories, decimal? targetCalories)
    {
        if (targetCalories is not { } target || target <= 0 || dayCalories <= target * (1 + PlanRuleCheck.Tolerance)) return 1m;
        return Math.Max(Floor, Math.Round(target / dayCalories, 2, MidpointRounding.ToZero));
    }

    /// <summary>Grams scaled and rounded to 5 g, at least 5 g.</summary>
    public static decimal Grams(decimal grams, decimal factor) =>
        factor == 1m ? grams : Math.Max(5m, Math.Round(grams * factor / 5m, MidpointRounding.AwayFromZero) * 5m);

    /// <summary>Servings scaled and rounded to a quarter, at least a quarter.</summary>
    public static decimal Servings(decimal servings, decimal factor) =>
        factor == 1m ? servings : Math.Max(0.25m, Math.Round(servings * factor * 4m, MidpointRounding.AwayFromZero) / 4m);
}
