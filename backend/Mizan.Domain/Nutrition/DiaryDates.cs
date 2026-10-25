namespace Mizan.Domain.Nutrition;

/// <summary>
/// The days a meal or a workout can be logged on. The log reaches ten years back, for a person
/// who catches up or brings their history along, and one year ahead, for entries logged in
/// advance. A date beyond either is almost always a typo (2205 for 2025) and is refused, not filed
/// on a day the person will never open.
/// </summary>
public static class DiaryDates
{
    public const int YearsBack = 10;
    public const int YearsAhead = 1;

    public const string Meal = "A meal";
    public const string Workout = "A workout";

    /// <summary>Why <paramref name="date"/> cannot hold the entry, or null when it can. <paramref name="entry"/> names it, as <see cref="Meal"/> or <see cref="Workout"/>.</summary>
    public static string? Problem(DateOnly date, DateOnly today, string entry = Meal)
    {
        if (date < today.AddYears(-YearsBack))
            return $"{entry} can be logged up to {YearsBack} years back. Check the date {date:yyyy-MM-dd}.";
        if (date > today.AddYears(YearsAhead))
            return $"{entry} can be logged up to a year ahead. Check the date {date:yyyy-MM-dd}.";
        return null;
    }
}
