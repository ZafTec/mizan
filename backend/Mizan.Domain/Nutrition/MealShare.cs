namespace Mizan.Domain.Nutrition;

/// <summary>
/// One person's part of a meal that several people ate together. The rule
/// lives here once, so a bot, a form, and an assistant tool all log the same
/// third of the same stew.
/// </summary>
public static class MealShare
{
    /// <summary>Two people is the smallest share; past ten the portions are guesses anyway.</summary>
    public const int MinPeople = 2;
    public const int MaxPeople = 10;

    public static bool IsValidPeople(int people) => people is >= MinPeople and <= MaxPeople;

    /// <summary>Calories are kept to whole numbers, as they are everywhere else in the log.</summary>
    public static decimal? OfCalories(decimal? whole, int people) =>
        whole is { } value ? Math.Round(value / people, 0) : null;

    /// <summary>Grams keep one decimal: a tenth of a gram is as fine as any label goes.</summary>
    public static decimal? OfGrams(decimal? whole, int people) =>
        whole is { } value ? Math.Round(value / people, 1) : null;

    public static string Suffix(int people) => $"(1/{people} share)";

    /// <summary>
    /// "Pasta" becomes "Pasta (1/3 share)"; a name that already says so is left
    /// alone, so a caller that labelled it first does not get it twice.
    /// </summary>
    public static string Name(string name, int people)
    {
        var suffix = Suffix(people);
        var trimmed = name.Trim();
        return trimmed.EndsWith(suffix, StringComparison.Ordinal) ? trimmed : $"{trimmed} {suffix}".Trim();
    }
}
