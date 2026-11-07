using System.Globalization;
using System.Text.RegularExpressions;

namespace Mizan.Prices.Scraping;

/// <summary>
/// Reads "500g", "1.5 L" or "10pc" off the end of a product name. Units come
/// out in the price book's vocabulary (g, kg, ml, l, piece). Anything else is
/// null and the alias the administrator curates supplies the quantity.
/// </summary>
public static partial class QuantityParser
{
    public static (decimal Quantity, string Unit)? Parse(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName)) return null;

        var match = Trailing().Match(productName.Trim());
        if (!match.Success) return null;

        var number = match.Groups["qty"].Value.Replace(',', '.');
        if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            return null;
        }

        var unit = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "kg" or "kgs" or "kilo" or "kilos" => "kg",
            "g" or "gm" or "gms" or "gr" or "gram" or "grams" => "g",
            "l" or "lt" or "ltr" or "ltrs" or "litre" or "litres" or "liter" or "liters" => "l",
            "ml" or "mls" => "ml",
            "pc" or "pcs" or "piece" or "pieces" or "pack" or "pk" => "piece",
            _ => null,
        };

        return unit is null ? null : (quantity, unit);
    }

    [GeneratedRegex(
        @"(?:^|[\s\-(,/])(?<qty>\d+(?:[.,]\d+)?)\s*(?<unit>kgs?|kilos?|g|gms?|gr|grams?|l|lt|ltrs?|litres?|liters?|mls?|pcs?|pieces?|pack|pk)\.?\)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Trailing();
}
