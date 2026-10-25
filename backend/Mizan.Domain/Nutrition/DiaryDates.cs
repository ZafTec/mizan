namespace Mizan.Domain.Nutrition;

/// <summary>
/// The days a meal can be logged on. The diary reaches ten years back, for a person who catches
/// up or brings their history along, and one year ahead, for meals logged in advance. A date
/// beyond either is almost always a typo (2205 for 2025) and is refused, not filed on a day the
/// person will never open.
/// </summary>
public static class DiaryDates
{
    public const int YearsBack = 10;
    public const int YearsAhead = 1;

    /// <summary>Why <paramref name="date"/> cannot hold a meal, or null when it can.</summary>
    public static string? Problem(DateOnly date, DateOnly today)
    {
        if (date < today.AddYears(-YearsBack))
            return $"A meal can be logged up to {YearsBack} years back. Check the date {date:yyyy-MM-dd}.";
        if (date > today.AddYears(YearsAhead))
            return $"A meal can be logged up to a year ahead. Check the date {date:yyyy-MM-dd}.";
        return null;
    }
}
