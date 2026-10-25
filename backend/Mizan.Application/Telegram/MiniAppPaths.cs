using System.Globalization;

namespace Mizan.Application.Telegram;

/// <summary>
/// The Mini App page for a notification's web link, for the <b>Open</b> button
/// under a notification sent to Telegram. Only pages the Mini App has are
/// mapped: a plan (<c>/meal-plan/{id}</c>, optionally <c>?day=yyyy-MM-dd</c>)
/// and a shopping list (<c>/shopping-lists/{id}</c>). Anything else answers
/// null, and the bot sends the text without a button.
/// </summary>
public static class MiniAppPaths
{
    public static string? ForLink(string? linkUrl)
    {
        if (string.IsNullOrWhiteSpace(linkUrl)) return null;

        var query = linkUrl.IndexOf('?');
        var path = (query < 0 ? linkUrl : linkUrl[..query]).TrimEnd('/');
        var segments = path.Split('/');
        if (segments is not ["", var page, var id] || !Guid.TryParse(id, out var guid)) return null;

        return page switch
        {
            "meal-plan" => Day(query < 0 ? string.Empty : linkUrl[(query + 1)..]) is { } day
                ? $"plan?planId={guid:D}&day={day:yyyy-MM-dd}"
                : $"plan?planId={guid:D}",
            "shopping-lists" => $"shopping-list?listId={guid:D}",
            _ => null,
        };
    }

    private static DateOnly? Day(string query)
    {
        foreach (var pair in query.Split('&'))
        {
            if (pair.StartsWith("day=", StringComparison.Ordinal)
                && DateOnly.TryParseExact(pair["day=".Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                return day;
            }
        }
        return null;
    }
}
