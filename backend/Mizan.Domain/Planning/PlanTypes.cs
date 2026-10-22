namespace Mizan.Domain.Planning;

/// <summary>A personal plan has dates and an owner. A template has neither a start date nor entries that are logged.</summary>
public enum PlanKind
{
    Personal,
    Template,
}

/// <summary>
/// A plan a coach drafts for a client starts as <see cref="Draft"/>. The client
/// accepts it to make it <see cref="Active"/>. A person has one active plan at a
/// time; accepting a new one archives the old one.
/// </summary>
public enum PlanStatus
{
    Draft,
    Active,
    Archived,
}

/// <summary>What a plan entry points at.</summary>
public enum PlanEntryKind
{
    /// <summary>A recipe and a number of servings. Nutrition is per serving times servings.</summary>
    Recipe,

    /// <summary>A catalogue food and a weight. Nutrition is per 100 g times grams / 100.</summary>
    Food,

    /// <summary>A plain line with no nutrition, listed as unresolved.</summary>
    Text,
}

/// <summary>The macro mode of a plan. It sets default targets that explicit targets override.</summary>
public enum MacroMode
{
    None,
    Keto,
    HighProtein,
    Custom,
}

/// <summary>Whether the person cooks every day or in batch sessions.</summary>
public enum PrepStyle
{
    Daily,
    Batch,
}

/// <summary>The slots a plan entry can take. One vocabulary with the diary's <c>MealTypes</c>.</summary>
public static class PlanSlots
{
    public static readonly string[] All =
    [
        Constants.MealTypes.Breakfast,
        Constants.MealTypes.Lunch,
        Constants.MealTypes.Dinner,
        Constants.MealTypes.Snack,
    ];

    public static bool IsValid(string? slot) =>
        slot is not null && All.Contains(slot.ToUpperInvariant());

    public static string Normalize(string? slot) =>
        string.IsNullOrWhiteSpace(slot) ? Constants.MealTypes.Dinner : slot.ToUpperInvariant();

    /// <summary>The order the slots appear in on a day.</summary>
    public static int Order(string slot) => Array.IndexOf(All, Normalize(slot)) is var i and >= 0 ? i : All.Length;
}
