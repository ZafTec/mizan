using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Measurements;
using Mizan.Telegram.Backend;

namespace Mizan.Telegram.Bot;

/// <summary>
/// One update in, zero or more Telegram calls out.
///
/// The routing is deliberately shallow: resolve the chat to a user, or refuse.
/// Everything past that point is the API's decision, not this service's - it
/// formats a request, formats a reply, and holds no opinion in between.
/// </summary>
public sealed class UpdateHandler
{
    /// <summary>Telegram compresses photos hard; anything above this is not a meal photo.</summary>
    private const int MaxPhotoBytes = 4 * 1024 * 1024;

    private readonly TelegramClient _telegram;
    private readonly MizanApiClient _api;
    private readonly TelegramBotOptions _options;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(
        TelegramClient telegram,
        MizanApiClient api,
        IOptions<TelegramBotOptions> options,
        ILogger<UpdateHandler> logger)
    {
        _telegram = telegram;
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        try
        {
            if (update.CallbackQuery is { } callback) await OnCallbackAsync(callback, ct);
            else if (update.Message is { } message) await OnMessageAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never the message text or the user id: the log is not the place
            // for either. The update id is enough to correlate.
            _logger.LogError(ex, "Failed to handle update {UpdateId}", update.UpdateId);

            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
            if (chatId is { } id)
            {
                await _telegram.SendMessageAsync(id, "Something went wrong on my side. Try again in a moment.", ct: ct);
            }
        }
    }

    // ---- Messages ---------------------------------------------------------

    private async Task OnMessageAsync(Message message, CancellationToken ct)
    {
        if (message.From is null || message.From.IsBot) return;

        // Personal nutrition data in a group is a leak with extra steps
        // (docs/REFOCUS.md §13). Refused outright, and said out loud so it
        // does not look broken.
        if (message.Chat.Type != "private")
        {
            await _telegram.SendMessageAsync(
                message.Chat.Id,
                "I only work in a direct message - your food log is not a group activity.",
                ct: ct);
            return;
        }

        var text = (message.Text ?? message.Caption ?? string.Empty).Trim();

        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            await OnStartAsync(message, text, ct);
            return;
        }

        var user = await _api.ResolveAsync(message.From.Id, ct);
        if (user is null)
        {
            await SendSignInPromptAsync(message.Chat.Id, ct);
            return;
        }

        if (message.Photo is { Count: > 0 })
        {
            await OnPhotoAsync(message, user, ct);
            return;
        }

        if (text.StartsWith('/'))
        {
            await OnCommandAsync(message.Chat.Id, message.From.Id, user, text, ct);
            return;
        }

        if (text.Length == 0) return;

        await OnChatAsync(message.Chat.Id, user, text, ct);
    }

    /// <summary>
    /// The only thing an unlinked chat can do. A bare /start is somebody who
    /// found the bot before the app, so it points them at the web rather than
    /// inventing a sign-up flow in a chat window.
    /// </summary>
    private async Task OnStartAsync(Message message, string text, CancellationToken ct)
    {
        var code = text.Length > "/start".Length ? text["/start".Length..].Trim() : string.Empty;
        var from = message.From!;

        if (code.Length == 0)
        {
            var existing = await _api.ResolveAsync(from.Id, ct);

            if (existing is not null)
            {
                await _telegram.SendMessageAsync(
                    message.Chat.Id, Greeting(existing.Name), ct: ct);
                return;
            }

            await SendSignInPromptAsync(message.Chat.Id, ct);
            return;
        }

        var outcome = await _api.LinkAsync(code, from.Id, from.Username, ct);

        if (!outcome.Linked)
        {
            await _telegram.SendMessageAsync(
                message.Chat.Id,
                $"{Escape(outcome.Problem ?? "That link did not work.")}\n\n"
                + $"Get a fresh one at {SettingsUrl()} - they last five minutes.",
                ct: ct);
            return;
        }

        await _telegram.SendMessageAsync(message.Chat.Id, Greeting(outcome.Name), ct: ct);
    }

    private async Task OnCommandAsync(
        long chatId, long telegramUserId, ResolvedUser user, string text, CancellationToken ct)
    {
        var space = text.IndexOf(' ');
        var command = (space < 0 ? text : text[..space]).ToLowerInvariant();
        var argument = space < 0 ? string.Empty : text[(space + 1)..].Trim();

        // A command sent to a group-aware bot arrives as /today@mizanbot.
        var at = command.IndexOf('@');
        if (at > 0) command = command[..at];

        switch (command)
        {
            case "/today":
                await OnTodayAsync(chatId, user, ct);
                return;

            case "/weight":
                await OnWeightAsync(chatId, user, argument, ct);
                return;

            case "/unlink":
                await _api.UnlinkAsync(telegramUserId, ct);
                await _telegram.SendMessageAsync(
                    chatId,
                    $"Disconnected. Your log is untouched - reconnect any time at {SettingsUrl()}.",
                    ct: ct);
                return;

            case "/help":
                await _telegram.SendMessageAsync(chatId, HelpText(), ct: ct);
                return;

            default:
                // Not a command: the assistant gets it. Somebody typing
                // "/protein target?" meant a question, not a syntax error.
                await OnChatAsync(chatId, user, text, ct);
                return;
        }
    }

    // ---- The assistant ----------------------------------------------------

    /// <summary>
    /// Same threads as the website, so a conversation started here continues in
    /// the browser. The most recent thread is the one it lands in; a new user
    /// gets a new one.
    /// </summary>
    private async Task OnChatAsync(long chatId, ResolvedUser user, string text, CancellationToken ct)
    {
        await _telegram.SendTypingAsync(chatId, ct);

        var threads = await _api.GetAsAsync<List<ThreadSummary>>(user.UserId, "api/Ai/threads?take=1", ct);
        var threadId = threads is { Count: > 0 } ? threads[0].Id : (Guid?)null;

        var result = await _api.SendForResultAsync<ChatTurn>(
            user.UserId, HttpMethod.Post, "api/Ai/chat", new { threadId, message = text }, ct);

        if (result is { Ok: true, Value.Reply.Content: { Length: > 0 } reply })
        {
            // The model answers in markdown, same as it does on the website.
            // Escaping it raw showed people literal asterisks.
            await _telegram.SendMessageAsync(chatId, TelegramMarkdown.ToHtml(reply), ct: ct);
            return;
        }

        await _telegram.SendMessageAsync(chatId, Escape(ExplainFailure(result)), ct: ct);
    }

    // ---- Logging ----------------------------------------------------------

    private async Task OnPhotoAsync(Message message, ResolvedUser user, CancellationToken ct)
    {
        // Telegram sends every size it made; the last is the largest.
        var photo = message.Photo!.MaxBy(p => p.Width * p.Height)!;

        await _telegram.SendTypingAsync(message.Chat.Id, ct);

        var bytes = await _telegram.DownloadAsync(photo.FileId, MaxPhotoBytes, ct);
        if (bytes is null)
        {
            await _telegram.SendMessageAsync(
                message.Chat.Id, "I could not read that photo. Try sending it again.", ct: ct);
            return;
        }

        var result = await _api.PostImageAsAsync<FoodAnalysis>(
            user.UserId, "api/Nutrition/ai/analyze-image", "image", bytes, "meal.jpg", ct);

        if (!result.Ok || result.Value is null)
        {
            await _telegram.SendMessageAsync(message.Chat.Id, Escape(ExplainFailure(result)), ct: ct);
            return;
        }

        var analysis = result.Value;
        if (analysis.Foods.Count == 0)
        {
            await _telegram.SendMessageAsync(
                message.Chat.Id, "I could not tell what that was. Try describing it instead.", ct: ct);
            return;
        }

        // A proposal, never a silent write - same rule as everywhere else.
        await _telegram.SendMessageAsync(
            message.Chat.Id,
            AnalysisCard(analysis),
            ConfirmKeyboard(analysis),
            ct);
    }

    private async Task OnTodayAsync(long chatId, ResolvedUser user, CancellationToken ct)
    {
        var day = await _api.GetAsAsync<DailyNutrition>(user.UserId, "api/Nutrition/daily", ct);

        if (day is null)
        {
            await _telegram.SendMessageAsync(chatId, "I could not read today's totals.", ct: ct);
            return;
        }

        var text = new StringBuilder()
            .AppendLine("<b>Today</b>")
            .AppendLine(Line("Calories", day.TotalCalories, day.TargetCalories, "kcal"))
            .AppendLine(Line("Protein", day.TotalProtein, day.TargetProtein, "g"))
            .AppendLine(Line("Carbs", day.TotalCarbs, day.TargetCarbs, "g"))
            .AppendLine(Line("Fat", day.TotalFat, day.TargetFat, "g"))
            .ToString();

        await _telegram.SendMessageAsync(chatId, text, ct: ct);
    }

    private async Task OnWeightAsync(long chatId, ResolvedUser user, string argument, CancellationToken ct)
    {
        if (!decimal.TryParse(argument.Replace("kg", "", StringComparison.OrdinalIgnoreCase).Trim(),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var kg) || kg is <= 0 or > 500)
        {
            await _telegram.SendMessageAsync(chatId, "Send it as <code>/weight 82.4</code>.", ct: ct);
            return;
        }

        var result = await _api.LogMeasurementAsync(
            user.UserId,
            new LogMeasurementRequest(null, kg, null, null, null, null, null, null, null, null, null, null),
            ct);

        await _telegram.SendMessageAsync(
            chatId,
            result.Ok ? $"Logged {kg:0.#} kg." : Escape(ExplainFailure(result)),
            ct: ct);
    }

    // ---- Inline keyboards -------------------------------------------------

    private async Task OnCallbackAsync(CallbackQuery callback, CancellationToken ct)
    {
        await _telegram.AnswerCallbackAsync(callback.Id, ct: ct);

        if (callback.From is null || callback.Message is null) return;

        var chatId = callback.Message.Chat.Id;
        await _telegram.ClearKeyboardAsync(chatId, callback.Message.MessageId, ct);

        var user = await _api.ResolveAsync(callback.From.Id, ct);
        if (user is null)
        {
            await SendSignInPromptAsync(chatId, ct);
            return;
        }

        var data = callback.Data ?? string.Empty;

        if (data == "discard")
        {
            await _telegram.SendMessageAsync(chatId, "Discarded. Nothing was logged.", ct: ct);
            return;
        }

        if (!data.StartsWith("log:", StringComparison.Ordinal))
        {
            return;
        }

        // The card carries its own numbers, because Telegram gives us 64 bytes
        // of callback data and no server-side state. Re-parsed here rather
        // than cached, so a button pressed an hour later still means what it
        // said - and the API validates it regardless.
        if (!TryParseLog(data, out var request))
        {
            await _telegram.SendMessageAsync(
                chatId, "That card is too old to use. Send the photo again.", ct: ct);
            return;
        }

        var result = await _api.SendForResultAsync<object>(
            user.UserId, HttpMethod.Post, "api/Meals", request, ct);

        await _telegram.SendMessageAsync(
            chatId,
            result.Ok ? $"Logged {Escape(request.Name)}." : Escape(ExplainFailure(result)),
            ct: ct);
    }

    /// <summary>
    /// Confirm / Discard. The totals ride in the callback data, rounded to
    /// whole numbers so they fit inside Telegram's 64-byte limit.
    /// </summary>
    private static object ConfirmKeyboard(FoodAnalysis analysis)
    {
        var name = string.Join(", ", analysis.Foods.Select(f => f.Name)).Replace('|', '/');
        if (name.Length > 24) name = name[..24];

        var protein = analysis.Foods.Sum(f => f.Protein);
        var carbs = analysis.Foods.Sum(f => f.Carbs);
        var fat = analysis.Foods.Sum(f => f.Fat);

        var payload =
            $"log:{Math.Round(analysis.TotalCalories)}|{Math.Round(protein)}|{Math.Round(carbs)}|{Math.Round(fat)}|{name}";

        return new
        {
            inline_keyboard = new[]
            {
                new object[]
                {
                    new { text = "Log it", callback_data = payload },
                    new { text = "Discard", callback_data = "discard" },
                },
            },
        };
    }

    private static bool TryParseLog(string data, out LoggedMeal request)
    {
        request = default!;

        var parts = data["log:".Length..].Split('|');
        if (parts.Length != 5) return false;

        if (!decimal.TryParse(parts[0], CultureInfo.InvariantCulture, out var calories)) return false;
        if (!decimal.TryParse(parts[1], CultureInfo.InvariantCulture, out var protein)) return false;
        if (!decimal.TryParse(parts[2], CultureInfo.InvariantCulture, out var carbs)) return false;
        if (!decimal.TryParse(parts[3], CultureInfo.InvariantCulture, out var fat)) return false;

        request = new LoggedMeal(
            parts[4].Length == 0 ? "Meal" : parts[4], calories, protein, carbs, fat);
        return true;
    }

    // ---- Text -------------------------------------------------------------

    private async Task SendSignInPromptAsync(long chatId, CancellationToken ct) =>
        await _telegram.SendMessageAsync(
            chatId,
            "This chat is not connected to a Mizan account yet.\n\n"
            + $"1. Sign in at {_options.PublicUrl.TrimEnd('/')}\n"
            + "2. Open <b>Settings → Telegram</b>\n"
            + "3. Tap <b>Connect</b> and follow the link back here\n\n"
            + "No account? Sign up on the same page - it takes a minute.",
            ct: ct);

    private static string Greeting(string? name) =>
        (name is { Length: > 0 } ? $"Connected. Hello {Escape(name)}.\n\n" : "Connected.\n\n") + HelpText();

    private static string HelpText() =>
        "Send me a photo of a meal and I will estimate it - you confirm before anything is logged.\n"
        + "Ask me anything the way you would on the website; it is the same conversation.\n\n"
        + "<b>/today</b> - totals against your targets\n"
        + "<b>/weight 82.4</b> - log a weigh-in\n"
        + "<b>/unlink</b> - disconnect this chat";

    private static string AnalysisCard(FoodAnalysis analysis)
    {
        var text = new StringBuilder("<b>Looks like:</b>\n");

        foreach (var food in analysis.Foods)
        {
            text.AppendLine(
                $"• {Escape(food.Name)} — {food.PortionGrams:0}g, {food.Calories:0} kcal");
        }

        text.AppendLine();
        text.AppendLine(
            $"<b>{analysis.TotalCalories:0} kcal</b> · "
            + $"P {analysis.Foods.Sum(f => f.Protein):0}g · "
            + $"C {analysis.Foods.Sum(f => f.Carbs):0}g · "
            + $"F {analysis.Foods.Sum(f => f.Fat):0}g");

        if (analysis.Note is { Length: > 0 })
        {
            text.AppendLine().Append("<i>").Append(Escape(analysis.Note)).Append("</i>");
        }

        return text.ToString();
    }

    private static string Line(string label, decimal actual, decimal? target, string unit) =>
        target is { } goal and > 0
            ? $"{label}: <b>{actual:0}</b> / {goal:0} {unit}  ({actual / goal:P0})"
            : $"{label}: <b>{actual:0}</b> {unit}";

    /// <summary>
    /// Turns an API failure into something a person can act on. A quota or
    /// upgrade wall is the common one and deserves better than "failed".
    /// </summary>
    private string ExplainFailure<T>(ApiResult<T> result) => result.Status switch
    {
        HttpStatusCode.Forbidden => result.Problem
            ?? $"That needs a Pro plan. Manage it at {_options.PublicUrl.TrimEnd('/')}/billing.",
        HttpStatusCode.TooManyRequests => result.Problem
            ?? "You have used today's assistant allowance. It resets tomorrow.",
        _ => result.Problem ?? "That did not work. Try again in a moment.",
    };

    private string SettingsUrl() => $"{_options.PublicUrl.TrimEnd('/')}/profile/settings/telegram";

    /// <summary>
    /// Messages go out as HTML, and every one of them contains something a
    /// user or a model wrote. Escaped, or a reply containing a stray angle
    /// bracket fails to send at all.
    /// </summary>
    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ---- Wire shapes read back from the API -------------------------------

    private sealed record ThreadSummary(Guid Id, string Title, DateTime UpdatedAt);

    private sealed record DailyNutrition
    {
        public decimal TotalCalories { get; init; }
        public decimal TotalProtein { get; init; }
        public decimal TotalCarbs { get; init; }
        public decimal TotalFat { get; init; }
        public decimal? TargetCalories { get; init; }
        public decimal? TargetProtein { get; init; }
        public decimal? TargetCarbs { get; init; }
        public decimal? TargetFat { get; init; }
    }

    private sealed record ChatTurn(Guid ThreadId, string Title, ChatMessage Reply);

    private sealed record ChatMessage(Guid Id, bool FromUser, string Content, DateTime CreatedAt);

    private sealed record FoodAnalysis
    {
        public List<RecognizedFood> Foods { get; init; } = [];
        public decimal TotalCalories { get; init; }
        public decimal Confidence { get; init; }
        public string? Note { get; init; }
    }

    private sealed record RecognizedFood
    {
        public string Name { get; init; } = string.Empty;
        public decimal PortionGrams { get; init; }
        public decimal Calories { get; init; }
        public decimal Protein { get; init; }
        public decimal Carbs { get; init; }
        public decimal Fat { get; init; }
    }

    private sealed record LoggedMeal(
        string Name, decimal Calories, decimal ProteinGrams, decimal CarbsGrams, decimal FatGrams)
    {
        public string MealType => "SNACK";
        public decimal Servings => 1;
    }
}
