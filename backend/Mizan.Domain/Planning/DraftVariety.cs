using System.Globalization;

namespace Mizan.Domain.Planning;

/// <summary>
/// A generated draft should not serve the same food or recipe at the same meal most days (issue #137). The
/// check is for drafts the assistant writes, so the repair call can ask for variety; a plan a person or a
/// coach writes may repeat what it likes. A slot may repeat an item on up to a third of the days, rounded up
/// and at least two; with batch cooking, where a cooked batch is eaten for several days, up to half
/// (docs/PLANNING.md#ai-planning). Plans shorter than three days are not checked.
/// </summary>
public static class DraftVariety
{
    /// <summary>One entry of a draft: the day, the meal, what it serves (a food or recipe id), and its name.</summary>
    public readonly record struct Item(int DayIndex, string Slot, Guid Id, string Name);

    /// <summary>An item served at one meal on more days than the limit.</summary>
    public sealed record Repeat(string Slot, string Name, int Days, int Limit, int DurationDays)
    {
        public string Message => string.Create(CultureInfo.InvariantCulture,
            $"{Name} is at {Slot} on {Days} of {DurationDays} days; use it on at most {Limit} and vary the rest.");
    }

    public static int Limit(int durationDays, bool batch) =>
        Math.Max(2, (int)Math.Ceiling(durationDays / (batch ? 2m : 3m)));

    public static IReadOnlyList<Repeat> Repeats(IEnumerable<Item> items, int durationDays, bool batch)
    {
        if (durationDays < 3) return [];
        var limit = Limit(durationDays, batch);

        return items
            .Where(i => i.DayIndex >= 0 && i.DayIndex < durationDays)
            .GroupBy(i => (Slot: i.Slot.ToLowerInvariant(), i.Id))
            .Select(g => new Repeat(g.Key.Slot, g.First().Name, g.Select(i => i.DayIndex).Distinct().Count(), limit, durationDays))
            .Where(r => r.Days > limit)
            .OrderByDescending(r => r.Days)
            .ThenBy(r => PlanSlots.Order(r.Slot))
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
