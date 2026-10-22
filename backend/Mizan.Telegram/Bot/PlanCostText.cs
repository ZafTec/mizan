using System.Globalization;
using Mizan.Contracts.Planning;

namespace Mizan.Telegram.Bot;

/// <summary>
/// The cost lines of the /plan and /shop replies, as plain text: escape them
/// with the rest of the message. Every number comes from the API; nothing here
/// adds or prices anything. A line is null when the API sent no cost, so a
/// region without prices shows no cost line at all.
/// </summary>
public static class PlanCostText
{
    /// <summary>"Cost: 245.50 ETB" for a plan day, when the day has priced items.</summary>
    public static string? Day(PlanDayViewDto view) =>
        view.Day.Cost is { } cost ? $"Cost: {Money(cost, view.Currency)}" : null;

    /// <summary>"Total: 1,234.00 ETB (estimated), 2 items without a price" for a shopping list.</summary>
    public static string? List(ShoppingListDetailDto list)
    {
        if (list.TotalCost is not { } total) return null;
        var line = $"Total: {Money(total, list.Currency)}";
        if (string.Equals(list.CostConfidence, "Estimated", StringComparison.OrdinalIgnoreCase)) line += " (estimated)";
        if (list.UnpricedCount > 0) line += list.UnpricedCount == 1 ? ", 1 item without a price" : $", {list.UnpricedCount} items without a price";
        return line;
    }

    /// <summary>The price after an item's name, " · 120.00 ETB", or nothing.</summary>
    public static string Item(ShoppingListItemDto item, string currency) =>
        item.Cost is { } cost ? $" · {Money(cost, currency)}" : string.Empty;

    private static string Money(decimal amount, string currency) =>
        string.IsNullOrEmpty(currency)
            ? amount.ToString("N2", CultureInfo.InvariantCulture)
            : $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}";
}
