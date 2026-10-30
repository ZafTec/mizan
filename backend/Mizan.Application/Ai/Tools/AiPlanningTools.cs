using System.Globalization;
using System.Text;
using Mizan.Application.Planning;
using Mizan.Contracts.Ai;
using Mizan.Contracts.Planning;
using Mizan.Domain.Ai;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Ai.Tools;

/// <summary>
/// What the planning task tools tell the assistant's model and show the person
/// (docs/PLANNING.md#ai-planning): plain text with the ids the next call needs, and the typed
/// cards built from the same results.
/// </summary>
public static class AiPlanningTools
{
    public static PlanPreviewCardData Preview(GeneratedPlanDto draft) => new(
        draft.Plan.Id,
        draft.Plan.Name,
        draft.Plan.Status,
        draft.Plan.StartDate,
        draft.Plan.DurationDays,
        draft.Plan.EntryCount,
        draft.Unresolved.Count,
        draft.BudgetCeiling,
        draft.Cost.WeeklyTotal,
        draft.Cost.Currency,
        draft.Violations);

    /// <summary>What the model reads back: each draft with its ids, entries, cost and problems, in plain lines.</summary>
    internal static string DescribeGenerated(GenerateMealPlanResult result)
    {
        var text = new StringBuilder();
        foreach (var draft in result.Drafts)
        {
            var plan = draft.Plan;
            text.Append(CultureInfo.InvariantCulture,
                $"Drafted \"{plan.Name}\" (plan {plan.Id}, {plan.Status}): {plan.DurationDays} days, {plan.EntryCount} entries, "
                + $"{Money.Format(draft.Cost.WeeklyTotal, draft.Cost.Currency)} a week ({draft.Cost.PricedItems} of {draft.Cost.TotalItems} lines priced)");
            if (draft.BudgetCeiling is { } ceiling) text.Append(CultureInfo.InvariantCulture, $" against a {Money.Format(ceiling, plan.Rules.Currency ?? draft.Cost.Currency)} budget");
            text.Append(".\n");
            foreach (var day in plan.Days)
            {
                var entries = day.Slots.SelectMany(s => s.Entries).ToList();
                if (entries.Count == 0) continue;
                text.Append(CultureInfo.InvariantCulture, $"Day {day.DayIndex}: ")
                    .Append(string.Join("; ", entries.Select(Entry)))
                    .Append('\n');
            }
            // Code's verdict, not the model's: what the draft does not meet, with amounts and targets.
            text.Append("Checked: ").Append(draft.Note).Append('\n');
            foreach (var unmet in draft.Unmet)
                text.Append("Not met (days counted from 1): ").Append(unmet).Append('\n');
            foreach (var line in draft.Unresolved)
                text.Append(CultureInfo.InvariantCulture, $"To review, day {line.DayIndex} {line.Slot}: {line.Text} ({line.Reason})\n");
        }
        foreach (var skipped in result.Skipped)
            text.Append(CultureInfo.InvariantCulture, $"No draft for the {Money.Format(skipped.BudgetCeiling, null)} budget: {skipped.Reason}\n");
        text.Append("A draft is not active until the user accepts it in the app.");
        return text.ToString();
    }

    internal static string DescribeDay(PlanDayViewDto view)
    {
        var day = view.Day;
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"Day {day.DayIndex} ({view.Date:yyyy-MM-dd}) of \"{view.PlanName}\" (plan {view.PlanId}): ");
        var entries = day.Slots.SelectMany(s => s.Entries).ToList();
        text.Append(entries.Count == 0 ? "no entries" : string.Join("; ", entries.Select(Entry)));
        text.Append(CultureInfo.InvariantCulture,
            $". Totals: {day.Totals.Calories:0} kcal, P {day.Totals.ProteinGrams:0} g, C {day.Totals.CarbsGrams:0} g, F {day.Totals.FatGrams:0} g.");
        foreach (var violation in day.Violations) text.Append(" Violation: ").Append(violation.Message);
        return text.ToString();
    }

    internal static string Entry(PlanEntryDto entry)
    {
        var what = entry.Kind switch
        {
            "Recipe" => string.Create(CultureInfo.InvariantCulture, $"{entry.RecipeTitle} x{entry.Servings:0.##}"),
            "Food" => string.Create(CultureInfo.InvariantCulture, $"{entry.FoodName} {entry.Grams:0} g"),
            _ => $"\"{entry.Text}\" (text)",
        };
        return $"{entry.Slot} {entry.Id} {what}";
    }
}
