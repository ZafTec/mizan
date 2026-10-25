using System.Globalization;

namespace Mizan.Domain.Pricing;

/// <summary>
/// The one way an amount of money is written: the currency code, a space, then the amount with
/// thousands separators and two decimals, as in "ETB 1,245.50". The web's <c>formatMoney</c>
/// writes the same, so a figure reads the same in a violation, a Telegram card and on the page.
/// </summary>
public static class Money
{
    public static string Format(decimal amount, string? currency)
    {
        var figure = Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture);
        var sign = amount < 0 ? "-" : string.Empty;
        return string.IsNullOrWhiteSpace(currency)
            ? sign + figure
            : $"{sign}{currency.Trim().ToUpperInvariant()} {figure}";
    }
}
