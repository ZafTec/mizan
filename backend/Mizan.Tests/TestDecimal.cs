using System.Globalization;

namespace Mizan.Tests;

/// <summary>
/// Turns the double in an attribute's test data into the decimal a person would write.
/// Since .NET 11 a cast keeps every binary digit (0.12 becomes 0.11999…), so tests read the
/// shortest round-trip text instead.
/// </summary>
internal static class Dec
{
    public static decimal Of(double value) =>
        decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

    public static decimal? Of(double? value) => value is { } v ? Of(v) : null;
}
