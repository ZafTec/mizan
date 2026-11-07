using System.Globalization;
using System.Text.RegularExpressions;

namespace Mizan.Domain.Pricing;

/// <summary>A package size read from a product name, and the name without it.</summary>
public sealed record PackageSizeMatch(decimal Quantity, string Unit, string Name);

/// <summary>
/// Reads the package size a shop writes into a product name: "Teff flour 1kg", "Milk 1L",
/// "Eggs 6pc", "Rice (500 gm)", "Water 6 x 500ml". The units are the price book's
/// (<see cref="PriceUnits"/>), with the same spellings the price worker's parser reads; here the
/// size may stand anywhere in the name, and a count before "x" multiplies it. The name comes back
/// without the size, for matching against the catalogue. Anything else is null: the
/// administrator gives the quantity.
/// </summary>
public static partial class PackageSize
{
    public static PackageSizeMatch? Parse(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName)) return null;

        var match = Size().Match(productName);
        if (!match.Success) return null;
        if (!TryNumber(match.Groups["qty"].Value, out var quantity) || quantity <= 0) return null;
        if (match.Groups["count"].Success)
        {
            if (!TryNumber(match.Groups["count"].Value, out var count) || count <= 0 || count != decimal.Truncate(count)) return null;
            quantity *= count;
        }

        var unit = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "kg" or "kgs" or "kilo" or "kilos" => PriceUnits.Kilogram,
            "g" or "gm" or "gms" or "gr" or "gram" or "grams" => PriceUnits.Gram,
            "l" or "lt" or "ltr" or "ltrs" or "litre" or "litres" or "liter" or "liters" => PriceUnits.Litre,
            "ml" or "mls" => PriceUnits.Millilitre,
            "pc" or "pcs" or "piece" or "pieces" or "pack" or "pk" => PriceUnits.Piece,
            _ => null,
        };
        if (unit is null) return null;

        var rest = productName.Remove(match.Index, match.Length);
        var name = string.Join(' ', Brackets().Replace(rest, " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim(' ', '-', ',', '/');
        return new PackageSizeMatch(quantity, unit, name);
    }

    private static bool TryNumber(string value, out decimal number) =>
        decimal.TryParse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);

    [GeneratedRegex(
        @"(?<![\w.,])(?:(?<count>\d+)\s*[x×]\s*)?(?<qty>\d+(?:[.,]\d+)?)\s*(?<unit>kgs?|kilos?|gms?|grams?|gr|g|ltrs?|litres?|liters?|lt|l|mls?|pcs?|pieces?|pack|pk)\.?(?![\w])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Size();

    /// <summary>Brackets left empty once the size is taken out.</summary>
    [GeneratedRegex(@"\(\s*\)|\[\s*\]")]
    private static partial Regex Brackets();
}
