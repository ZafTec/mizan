using System.Globalization;

namespace Mizan.Domain.Nutrition;

/// <summary>A total against the target it is measured by. A null or zero target is "no target".</summary>
public readonly record struct NutrientAmount(string Label, decimal Actual, decimal? Target, string Unit);

/// <summary>
/// The one way totals are put into words. The Telegram bot, the assistant's
/// tools, and its context all read from here, so "1,850 of 2,000 kcal" means
/// the same thing wherever a person or a model meets it.
/// </summary>
public static class NutritionText
{
    public static IReadOnlyList<NutrientAmount> Daily(
        decimal calories, decimal protein, decimal carbs, decimal fat,
        decimal? targetCalories = null, decimal? targetProtein = null,
        decimal? targetCarbs = null, decimal? targetFat = null) =>
    [
        new("Calories", calories, targetCalories, "kcal"),
        new("Protein", protein, targetProtein, "g"),
        new("Carbs", carbs, targetCarbs, "g"),
        new("Fat", fat, targetFat, "g"),
    ];

    /// <summary>
    /// "Calories: 1850 / 2000 kcal (93%)", or "Calories: 1850 kcal" without a
    /// target. <paramref name="emphasize"/> wraps the actual number only, so a
    /// transport can bold it without owning the sentence.
    /// </summary>
    public static string Describe(NutrientAmount amount, Func<string, string>? emphasize = null)
    {
        var actual = Whole(amount.Actual);
        if (emphasize is not null) actual = emphasize(actual);

        return amount.Target is { } target and > 0
            ? $"{amount.Label}: {actual} / {Whole(target)} {amount.Unit} ({Percent(amount.Actual, target)})"
            : $"{amount.Label}: {actual} {amount.Unit}";
    }

    /// <summary>Every amount on one line, comma separated.</summary>
    public static string Summary(IEnumerable<NutrientAmount> amounts) =>
        string.Join(", ", amounts.Select(a => Describe(a)));

    /// <summary>
    /// What is left of each target: "Left: 650 kcal, 48 g protein, 120 g carbs, 0 g fat (8 g over)". An amount
    /// without a target is left out; an empty string means nothing has a target.
    /// </summary>
    public static string Remaining(IEnumerable<NutrientAmount> amounts)
    {
        var parts = amounts
            .Where(a => a.Target is > 0)
            .Select(a =>
            {
                var left = a.Target!.Value - a.Actual;
                var name = a.Unit == "kcal" ? "kcal" : $"{a.Unit} {a.Label.ToLowerInvariant()}";
                return left >= 0
                    ? $"{Whole(left)} {name}"
                    : $"0 {name} ({Whole(-left)} {a.Unit} over)";
            })
            .ToList();
        return parts.Count == 0 ? string.Empty : "Left: " + string.Join(", ", parts);
    }

    private static string Whole(decimal value) =>
        Math.Round(value, 0).ToString("0", CultureInfo.InvariantCulture);

    // Half a percent rounds up: 92.5% of a target reads as 93%, the way a person would say it.
    private static string Percent(decimal actual, decimal target) =>
        Math.Round(actual / target * 100m, 0, MidpointRounding.AwayFromZero)
            .ToString("0", CultureInfo.InvariantCulture) + "%";
}
