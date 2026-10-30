using System.Globalization;
using Mizan.Contracts.Nutrition;

namespace Mizan.Telegram.Bot;

/// <summary>
/// The buttons under a photo estimate. A food the API matched to one of the person's recipes is logged as
/// that recipe at the servings the API proposed (<c>lr:</c>); the other foods are logged together as one
/// meal (<c>log:</c>, <c>split:</c>). A stored photo gets <b>Delete the photo</b> (<c>dp</c>), whose address
/// rides in the card's text as a link, because a URL does not fit in 64 bytes of callback data.
///
/// The bot keeps no state: a pressed card says what else it offered (<see cref="Message.ReplyMarkup"/>), and
/// <see cref="Remaining"/> works out which of those still apply, so logging a recipe leaves the rest of the
/// meal and the photo button to press.
/// </summary>
internal static class PhotoCard
{
    public const string LogPrefix = "log:";
    public const string SplitPrefix = "split:";
    public const string SharePrefix = "share:";
    public const string RecipePrefix = "lr:";
    public const string DeletePhoto = "dp";
    public const string Discard = "discard";
    public const string Done = "done";

    /// <summary>The foods the API matched to one of the person's recipes, with the servings it proposed.</summary>
    public static List<RecognizedFood> Recipes(FoodAnalysisResult analysis) =>
        analysis.Foods.Where(f => f.Source == RecognizedFood.FromRecipeSource && f.RecipeId is not null && f.Servings is > 0).ToList();

    /// <summary>The photo's address when Telegram can link to it: https only, like every other button target.</summary>
    public static string? PhotoUrl(FoodAnalysisResult analysis) =>
        Uri.TryCreate(analysis.ImageUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps ? analysis.ImageUrl : null;

    /// <summary>The line that carries the photo's address, which <see cref="LinkIn"/> reads back when Delete is pressed.</summary>
    public static string PhotoLine(string url) =>
        $"<a href=\"{url.Replace("&", "&amp;").Replace("\"", "&quot;")}\">Your photo</a> is kept for 30 days.";

    /// <summary>The photo address a card carries, or null when it carries none.</summary>
    public static string? LinkIn(Message? message) =>
        message?.Entities?.FirstOrDefault(e => e.Type == "text_link" && e.Url is { Length: > 0 })?.Url;

    /// <summary>"Log Doro wat, 1.5 servings": what the API matched and proposed, nothing worked out here.</summary>
    public static string RecipeLabel(string title, decimal servings) =>
        $"Log {UpdateHandler.TruncateUtf8(title, 40)}, {Servings(servings)}";

    public static string Servings(decimal servings) =>
        servings == 1 ? "1 serving" : servings.ToString("0.##", CultureInfo.InvariantCulture) + " servings";

    /// <summary><c>lr:{recipeId}|{servings}</c>, at most 3 + 32 + 1 + 7 bytes.</summary>
    public static string RecipeData(Guid recipeId, decimal servings) =>
        $"{RecipePrefix}{recipeId:N}|{servings.ToString("0.##", CultureInfo.InvariantCulture)}";

    public static bool TryParseRecipe(string data, out Guid recipeId, out decimal servings)
    {
        recipeId = default;
        servings = 0;
        var parts = data.Split('|');
        return parts.Length == 2
            && Guid.TryParseExact(parts[0], "N", out recipeId)
            && decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out servings)
            && servings > 0;
    }

    /// <summary>
    /// A row per recipe match; then, for the foods that are not recipes, Log it and Split (named "the rest"
    /// beside a recipe), with Discard; then Delete the photo when the card links one.
    /// </summary>
    public static InlineKeyboardMarkup Keyboard(FoodAnalysisResult analysis, string? restData, bool photo)
    {
        var rows = new List<IReadOnlyList<InlineKeyboardButton>>();
        var recipes = Recipes(analysis);

        foreach (var recipe in recipes)
        {
            rows.Add([InlineKeyboardButton.Callback(RecipeLabel(recipe.Name, recipe.Servings!.Value), RecipeData(recipe.RecipeId!.Value, recipe.Servings.Value))]);
        }

        var rest = recipes.Count > 0 ? " the rest" : string.Empty;
        rows.Add(restData is null
            ? [InlineKeyboardButton.Callback("Discard", Discard)]
            :
            [
                InlineKeyboardButton.Callback("Log" + (rest.Length > 0 ? rest : " it"), LogPrefix + restData),
                InlineKeyboardButton.Callback("Split" + rest, SplitPrefix + restData),
                InlineKeyboardButton.Callback("Discard", Discard),
            ]);

        if (photo) rows.Add([InlineKeyboardButton.Callback("Delete the photo", DeletePhoto)]);
        return new InlineKeyboardMarkup { InlineKeyboard = rows };
    }

    /// <summary>
    /// What the card still offers once <paramref name="pressed"/> has been handled, or null when nothing:
    /// the pressed button goes; logging the rest of the meal (or one share of it) takes Log and Split with it;
    /// Discard and Done take every logging button; once something is logged, Discard becomes "That's all";
    /// and Discard goes when nothing is left to log.
    /// </summary>
    public static InlineKeyboardMarkup? Remaining(InlineKeyboardMarkup? card, string pressed)
    {
        if (card is null) return null;

        static bool IsRest(string data) => data.StartsWith(LogPrefix, StringComparison.Ordinal)
            || data.StartsWith(SplitPrefix, StringComparison.Ordinal) || data.StartsWith(SharePrefix, StringComparison.Ordinal);
        static bool IsLogging(string data) => IsRest(data) || data.StartsWith(RecipePrefix, StringComparison.Ordinal);
        static bool IsEnd(string data) => data is Discard or Done;

        var logged = IsLogging(pressed);
        var buttons = card.InlineKeyboard
            .Select(row => row.Where(b => b.CallbackData is { } data
                && data != pressed
                && !(IsRest(pressed) && IsRest(data))
                && !(IsEnd(pressed) && IsLogging(data))).ToList())
            .ToList();

        var anyLogging = buttons.SelectMany(r => r).Any(b => IsLogging(b.CallbackData!));
        var rows = buttons
            .Select(row => (IReadOnlyList<InlineKeyboardButton>)row
                .Where(b => anyLogging || !IsEnd(b.CallbackData!))
                .Select(b => logged && b.CallbackData == Discard ? InlineKeyboardButton.Callback("That's all", Done) : b)
                .ToList())
            .Where(row => row.Count > 0)
            .ToList();

        return rows.Count == 0 ? null : new InlineKeyboardMarkup { InlineKeyboard = rows };
    }

    /// <summary>Whether a keyboard still has Delete the photo, so the text that carries its address must go with it.</summary>
    public static bool HasDelete(InlineKeyboardMarkup? keyboard) =>
        keyboard?.InlineKeyboard.SelectMany(r => r).Any(b => b.CallbackData == DeletePhoto) == true;

    /// <summary>The rows of <paramref name="card"/> that are recipe or photo buttons, carried under the Split picker.</summary>
    public static IEnumerable<IReadOnlyList<InlineKeyboardButton>> CarriedUnderSplit(InlineKeyboardMarkup? card) =>
        card?.InlineKeyboard.Where(row => row.Any(b => b.CallbackData is { } data
            && (data.StartsWith(RecipePrefix, StringComparison.Ordinal) || data == DeletePhoto))) ?? [];
}
