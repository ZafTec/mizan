namespace Mizan.Domain.Constants;

/// <summary>
/// Canonical meal-type vocabulary, stored uppercase. <see cref="Meal"/> is a legacy
/// value retained so historical diary rows remain valid; new UIs offer the others.
/// </summary>
public static class MealTypes
{
    public const string Breakfast = "BREAKFAST";
    public const string Lunch = "LUNCH";
    public const string Dinner = "DINNER";
    public const string Snack = "SNACK";
    public const string Drink = "DRINK";
    public const string Meal = "MEAL";

    public static readonly string[] All = { Breakfast, Lunch, Dinner, Snack, Drink, Meal };

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value.ToUpperInvariant());

    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Snack : value.ToUpperInvariant();
}
