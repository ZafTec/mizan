namespace Mizan.Contracts.Telegram;

/// <summary>
/// The Mini App's pages, relative to <c>TelegramBot:MiniAppUrl</c> (by default <c>{PublicUrl}/tg</c>). Every
/// <c>web_app</c> button and every notification's <b>Open</b> button points at one of these, and the web app serves
/// each under <c>frontend/app/tg</c> (docs/TELEGRAM.md#mini-app).
/// </summary>
public static class MiniAppPages
{
    /// <summary><c>/tg</c>: the Mini App's home, for the menu button, <c>/start</c> and <c>/help</c>.</summary>
    public const string Home = "";

    /// <summary><c>/tg/plan</c>, optionally with <c>?planId=</c> and <c>&amp;day=yyyy-MM-dd</c>.</summary>
    public const string Plan = "plan";

    /// <summary><c>/tg/shopping-list</c>, optionally with <c>?listId=</c>.</summary>
    public const string ShoppingList = "shopping-list";

    public static IReadOnlyList<string> All { get; } = [Home, Plan, ShoppingList];

    /// <summary>The page a Mini App path such as <c>plan?planId=...</c> opens.</summary>
    public static string PageOf(string path)
    {
        var query = path.IndexOf('?');
        return (query < 0 ? path : path[..query]).Trim('/');
    }
}
