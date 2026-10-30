using System.Globalization;

namespace Mizan.Domain.Planning;

/// <summary>
/// A generated draft is saved only when every day has at least one food or recipe entry: a day
/// of notes alone carries no nutrition or cost, so the plan would be held to nothing
/// (docs/PLANNING.md#ai-planning).
/// </summary>
public static class DraftFoodCoverage
{
    /// <summary>The days, in order, that have no food or recipe entry.</summary>
    public static IReadOnlyList<int> EmptyDays(IEnumerable<(int DayIndex, PlanEntryKind Kind)> entries, int durationDays)
    {
        var covered = entries
            .Where(e => e.Kind is PlanEntryKind.Food or PlanEntryKind.Recipe)
            .Select(e => e.DayIndex)
            .ToHashSet();
        return Enumerable.Range(0, Math.Max(0, durationDays)).Where(day => !covered.Contains(day)).ToList();
    }

    /// <summary>
    /// What the refusal says: which days are empty, counted from 1 as people do, and which
    /// foods the assistant named that the catalogue does not have.
    /// </summary>
    public static string Message(IReadOnlyList<int> emptyDays, int durationDays, IReadOnlyList<string> missingFoods)
    {
        var days = emptyDays.Count == durationDays
            ? "any day"
            : (emptyDays.Count == 1 ? "day " : "days ") + Join(emptyDays.Select(d => (d + 1).ToString(CultureInfo.InvariantCulture)).ToList());
        var text = $"No draft was saved: the catalogue has no foods for {days} of the plan.";
        if (missingFoods.Count > 0) text += $" Not in the catalogue: {Join(missingFoods)}.";
        return text + " Add those foods, or ask an administrator to import the starter foods, then try again.";
    }

    /// <summary>The refusal before anything is asked: there is nothing to plan with.</summary>
    public const string NothingToPlanWith =
        "There are no foods or recipes to plan with yet. Add foods, or ask an administrator to import the starter foods, then try again.";

    private static string Join(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };
}
