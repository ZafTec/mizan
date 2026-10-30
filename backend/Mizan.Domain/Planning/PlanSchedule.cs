namespace Mizan.Domain.Planning;

/// <summary>Entries carry a day index. Dates come from the plan's start date.</summary>
public static class PlanSchedule
{
    public static DateOnly DateOf(DateOnly startDate, int dayIndex) => startDate.AddDays(dayIndex);

    public static DateOnly EndDate(DateOnly startDate, int durationDays) => startDate.AddDays(Math.Max(durationDays, 1) - 1);

    /// <summary>The day index of a date inside the plan, or null when the date is outside it.</summary>
    public static int? DayIndexOf(DateOnly startDate, int durationDays, DateOnly date)
    {
        var index = date.DayNumber - startDate.DayNumber;
        return index >= 0 && index < durationDays ? index : null;
    }

    public static bool Covers(DateOnly startDate, int durationDays, DateOnly date) =>
        DayIndexOf(startDate, durationDays, date).HasValue;
}
