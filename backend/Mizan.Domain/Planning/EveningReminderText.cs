using System.Globalization;

namespace Mizan.Domain.Planning;

/// <summary>
/// The evening reminder's one line, the same in the app, on the phone and in
/// Telegram: "Your window closes at 20:00. Today: 1,640 of 1,800 kcal." Without
/// a calorie target the line ends at the day's total.
/// </summary>
public static class EveningReminderText
{
    public static string Build(TimeOnly windowEnd, decimal caloriesToday, decimal? targetCalories)
    {
        var today = Kcal(caloriesToday);
        var totals = targetCalories is { } target ? $"{today} of {Kcal(target)} kcal" : $"{today} kcal";
        return $"Your window closes at {windowEnd.ToString("HH:mm", CultureInfo.InvariantCulture)}. Today: {totals}.";
    }

    private static string Kcal(decimal value) =>
        Math.Round(Math.Max(value, 0m), MidpointRounding.AwayFromZero).ToString("#,0", CultureInfo.InvariantCulture);
}
