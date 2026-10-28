using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Ai;
using Mizan.Contracts.Meals;
using Mizan.Contracts.Measurements;
using Mizan.Contracts.Nutrition;
using Mizan.Contracts.Planning;
using Mizan.Contracts.Telegram;
using Mizan.Contracts.Users;
using Mizan.Domain.Nutrition;
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
    private readonly PlanMeHandler _planMe;

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
        _planMe = new PlanMeHandler(telegram, api, options.Value);
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
        // (docs/TELEGRAM.md). Refused outright, and said out loud so it
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

        // Telegram is a Pro feature. A lapsed account keeps its link and can
        // still disconnect; everything else points at the upgrade.
        if (!user.IsPro && !IsAlwaysAllowed(text))
        {
            await SendUpgradePromptAsync(message.Chat.Id, ct);
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
                    message.Chat.Id, Greeting(existing.Name), MiniAppKeyboard("Open Mizan"), ct);
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

        await _telegram.SendMessageAsync(message.Chat.Id, Greeting(outcome.Name), MiniAppKeyboard("Open Mizan"), ct);
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

            case "/plan":
                await OnPlanAsync(chatId, user, argument, ct);
                return;

            case "/shop":
                await OnShopAsync(chatId, user, page: 0, ct);
                return;

            case PlanMeHandler.Command:
                await _planMe.HandleCommandAsync(chatId, user, argument, ct);
                return;

            case "/notifications":
                await OnNotificationsAsync(chatId, user, argument, ct);
                return;

            case "/unlink":
                await _api.UnlinkAsync(telegramUserId, ct);
                await _telegram.SendMessageAsync(
                    chatId,
                    $"Disconnected. Your log is untouched - reconnect any time at {SettingsUrl()}.",
                    ct: ct);
                return;

            case "/help":
                await _telegram.SendMessageAsync(chatId, HelpText(), MiniAppKeyboard("Open Mizan"), ct);
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

        var threads = await _api.GetAsAsync<List<AiChatThreadDto>>(user.UserId, "api/Ai/threads?take=1", ct);
        var threadId = threads is { Count: > 0 } ? threads[0].Id : (Guid?)null;

        var result = await _api.SendForResultAsync<AiChatTurnDto>(
            user.UserId, HttpMethod.Post, "api/Ai/chat", new { threadId, message = text }, ct);

        if (result is { Ok: true, Value: { Reply.Content: { Length: > 0 } reply } turn })
        {
            // The model answers in markdown, same as it does on the website.
            // Escaping it raw showed people literal asterisks.
            await _telegram.SendMessageAsync(
                chatId, TelegramMarkdown.ToHtml(reply) + PerformedLines(turn.Performed), ct: ct);
            return;
        }

        await _telegram.SendMessageAsync(chatId, Escape(ExplainFailure(result)), ct: ct);
    }

    /// <summary>
    /// What the turn did to the log, one line each. The website shows these as
    /// chips; here they ride under the reply, so "make me a shopping list"
    /// ends with proof it happened rather than a sentence claiming it did.
    /// </summary>
    internal static string PerformedLines(IReadOnlyList<AiToolInvocation>? performed)
    {
        if (performed is not { Count: > 0 }) return string.Empty;

        var text = new StringBuilder("\n");
        foreach (var action in performed)
        {
            text.Append('\n').Append(action.Succeeded ? "✓ " : "✗ ").Append(Escape(action.Summary));
            if (!action.Succeeded && action.Error is { Length: > 0 } error)
            {
                text.Append(": ").Append(Escape(error));
            }
        }

        return text.ToString();
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

        var result = await _api.PostImageAsAsync<FoodAnalysisResult>(
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
        var day = await _api.GetAsAsync<DailyNutritionTotals>(user.UserId, "api/Nutrition/daily", ct);

        if (day is null)
        {
            await _telegram.SendMessageAsync(chatId, "I could not read today's totals.", ct: ct);
            return;
        }

        // The words are the domain's; only the bold is ours.
        var text = new StringBuilder().AppendLine("<b>Today</b>");
        foreach (var amount in NutritionText.Daily(
            day.TotalCalories, day.TotalProtein, day.TotalCarbs, day.TotalFat,
            day.TargetCalories, day.TargetProtein, day.TargetCarbs, day.TargetFat))
        {
            text.AppendLine(NutritionText.Describe(amount, actual => $"<b>{actual}</b>"));
        }

        await _telegram.SendMessageAsync(chatId, text.ToString(), ct: ct);
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

    // ---- Notifications ----------------------------------------------------

    /// <summary>
    /// Whether the app's notifications also come to this chat. Bare, it says
    /// which and offers one button to flip it; <c>on</c> and <c>off</c> set it.
    /// Both read and write <c>/api/Users/me</c>, the switch the settings page
    /// uses, so the API decides and the bot only reports.
    /// </summary>
    private async Task OnNotificationsAsync(long chatId, ResolvedUser user, string argument, CancellationToken ct)
    {
        switch (argument.ToLowerInvariant())
        {
            case "on":
                await SetNotificationsAsync(chatId, user, on: true, ct);
                return;
            case "off":
                await SetNotificationsAsync(chatId, user, on: false, ct);
                return;
            case "":
                break;
            default:
                await _telegram.SendMessageAsync(
                    chatId, "Send <code>/notifications on</code> or <code>/notifications off</code>.", ct: ct);
                return;
        }

        var me = await _api.SendForResultAsync<UserDto>(user.UserId, HttpMethod.Get, "api/Users/me", null, ct);
        if (me is not { Ok: true, Value: { } profile })
        {
            await _telegram.SendMessageAsync(chatId, Escape(ExplainFailure(me)), ct: ct);
            return;
        }

        await _telegram.SendMessageAsync(
            chatId,
            profile.NotifyByTelegram
                ? "Notifications are <b>on</b>: what the app notifies you about comes to this chat too."
                : "Notifications are <b>off</b>: they stay in the app.",
            InlineKeyboardMarkup.Column(profile.NotifyByTelegram
                ? InlineKeyboardButton.Callback("Turn off", "nt:off")
                : InlineKeyboardButton.Callback("Turn on", "nt:on")),
            ct);
    }

    private async Task SetNotificationsAsync(long chatId, ResolvedUser user, bool on, CancellationToken ct)
    {
        var result = await _api.SendForResultAsync<object>(
            user.UserId, HttpMethod.Put, "api/Users/me", new UpdateUserRequest(null, null, NotifyByTelegram: on), ct);

        await _telegram.SendMessageAsync(
            chatId,
            !result.Ok ? Escape(ExplainFailure(result))
            : on ? "Notifications are on. You will get them here as well as in the app. <b>/notifications off</b> stops them."
            : "Notifications are off. They stay in the app. <b>/notifications on</b> brings them back.",
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

        if (!user.IsPro)
        {
            await SendUpgradePromptAsync(chatId, ct);
            return;
        }

        var data = callback.Data ?? string.Empty;

        if (data == "discard")
        {
            await _telegram.SendMessageAsync(chatId, "Discarded. Nothing was logged.", ct: ct);
            return;
        }

        // "Split" asks how many people share the meal, then logs one share.
        if (data.StartsWith("split:", StringComparison.Ordinal))
        {
            if (!TryParseTotals(data["split:".Length..], out var whole))
            {
                await _telegram.SendMessageAsync(
                    chatId, "That card is too old to use. Send the photo again.", ct: ct);
                return;
            }

            await _telegram.SendMessageAsync(
                chatId,
                "<b>Split this meal</b>\nHow many people are sharing it? I will log your share only.",
                SplitKeyboard(data["split:".Length..]),
                ct);
            return;
        }

        if (data.StartsWith("share:", StringComparison.Ordinal))
        {
            await LogShareAsync(chatId, user, data["share:".Length..], ct);
            return;
        }

        // The plan and shopping cards. Each button carries one id, because two
        // guids do not fit in 64 bytes; the rest is read back from the API.
        if (data.StartsWith("pe:", StringComparison.Ordinal))
        {
            await LogPlanEntryAsync(chatId, user, data["pe:".Length..], ct);
            return;
        }

        if (data.StartsWith("pd:", StringComparison.Ordinal))
        {
            if (!TryParseDate(data["pd:".Length..], out var day))
            {
                await SendStaleCardAsync(chatId, "/plan", ct);
                return;
            }

            await SendPlanDayAsync(chatId, user, day, prefix: null, ct);
            return;
        }

        if (data.StartsWith("sh:", StringComparison.Ordinal))
        {
            await ToggleShoppingItemAsync(chatId, user, data["sh:".Length..], ct);
            return;
        }

        if (data.StartsWith("sp:", StringComparison.Ordinal))
        {
            if (!int.TryParse(data["sp:".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var page))
            {
                await SendStaleCardAsync(chatId, "/shop", ct);
                return;
            }

            await OnShopAsync(chatId, user, page, ct);
            return;
        }

        if (data is "nt:on" or "nt:off")
        {
            await SetNotificationsAsync(chatId, user, on: data == "nt:on", ct);
            return;
        }

        if (PlanMeHandler.IsCallback(data))
        {
            await _planMe.HandleCallbackAsync(chatId, user, data, ct);
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
    internal static object ConfirmKeyboard(FoodAnalysisResult analysis)
    {
        var protein = analysis.Foods.Sum(f => f.Protein);
        var carbs = analysis.Foods.Sum(f => f.Carbs);
        var fat = analysis.Foods.Sum(f => f.Fat);

        var numbers = $"{Math.Round(analysis.TotalCalories)}|{Math.Round(protein)}|{Math.Round(carbs)}|{Math.Round(fat)}|";

        // The longest button is "share:5|" plus this, and Telegram refuses the whole message over 64 bytes. The
        // name is cut by bytes, not characters, so a name in another script cannot push a button over the limit.
        var name = TruncateUtf8(string.Join(", ", analysis.Foods.Select(f => f.Name)).Replace('|', '/'), 64 - "share:5|".Length - numbers.Length);

        var payload = $"log:{numbers}{name}";

        return new
        {
            inline_keyboard = new[]
            {
                new object[]
                {
                    new { text = "Log it", callback_data = payload },
                    new { text = "Split", callback_data = "split:" + payload["log:".Length..] },
                    new { text = "Discard", callback_data = "discard" },
                },
            },
        };
    }

    /// <summary>People to choose from when a meal is shared. Larger groups can log it by hand on the website.</summary>
    internal static readonly int[] SplitChoices = [2, 3, 4, 5];

    /// <summary>
    /// The count picker. Each button carries the whole meal's totals and the number of people, so one tap logs
    /// one share and the bot needs no state. "Log it all" is there because a tap on Split can be a mistake.
    /// </summary>
    internal static object SplitKeyboard(string totals) => new
    {
        inline_keyboard = new[]
        {
            SplitChoices.Select(n => (object)new { text = n.ToString(CultureInfo.InvariantCulture), callback_data = $"share:{n}|{totals}" }).ToArray(),
            new object[]
            {
                new { text = "Log it all", callback_data = "log:" + totals },
                new { text = "Discard", callback_data = "discard" },
            },
        },
    };

    private async Task LogShareAsync(long chatId, ResolvedUser user, string data, CancellationToken ct)
    {
        var bar = data.IndexOf('|');
        if (bar < 1
            || !int.TryParse(data[..bar], NumberStyles.None, CultureInfo.InvariantCulture, out var people)
            || !SplitChoices.Contains(people)
            || !TryParseTotals(data[(bar + 1)..], out var whole))
        {
            await _telegram.SendMessageAsync(
                chatId, "That card is too old to use. Send the photo again.", ct: ct);
            return;
        }

        // The whole meal goes over the wire with the head count; the API divides
        // and labels the share, so this number means the same thing everywhere.
        var result = await _api.SendForResultAsync<object>(
            user.UserId, HttpMethod.Post, "api/Meals", whole with { SharedBetween = people }, ct);

        await _telegram.SendMessageAsync(
            chatId,
            result.Ok
                ? $"Logged your 1/{people} share of {Escape(whole.Name)}."
                : Escape(ExplainFailure(result)),
            ct: ct);
    }

    /// <summary>The longest prefix of the text that fits in the given number of UTF-8 bytes, never cutting a character.</summary>
    internal static string TruncateUtf8(string value, int maxBytes)
    {
        if (maxBytes <= 0) return string.Empty;
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;

        var builder = new StringBuilder();
        var used = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (used + size > maxBytes) break;
            builder.Append(rune.ToString());
            used += size;
        }

        return builder.ToString();
    }

    private static bool TryParseTotals(string data, out LogMealRequest request) =>
        TryParseLog("log:" + data, out request);

    /// <summary>The card's numbers back into the shared wire record: a whole meal, logged as one snack.</summary>
    internal static bool TryParseLog(string data, out LogMealRequest request)
    {
        request = default!;

        var parts = data["log:".Length..].Split('|');
        if (parts.Length != 5) return false;

        if (!decimal.TryParse(parts[0], CultureInfo.InvariantCulture, out var calories)) return false;
        if (!decimal.TryParse(parts[1], CultureInfo.InvariantCulture, out var protein)) return false;
        if (!decimal.TryParse(parts[2], CultureInfo.InvariantCulture, out var carbs)) return false;
        if (!decimal.TryParse(parts[3], CultureInfo.InvariantCulture, out var fat)) return false;

        request = new LogMealRequest
        {
            Name = parts[4].Length == 0 ? "Meal" : parts[4],
            MealType = "SNACK",
            Servings = 1,
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
        };
        return true;
    }

    // ---- Meal plan --------------------------------------------------------

    /// <summary>
    /// /plan, /plan tomorrow, /plan 2026-10-23. Without a date the API picks the
    /// user's today in their own time zone; the bot never works that out itself.
    /// </summary>
    private async Task OnPlanAsync(long chatId, ResolvedUser user, string argument, CancellationToken ct)
    {
        DateOnly? date = null;

        if (argument.Equals("tomorrow", StringComparison.OrdinalIgnoreCase))
        {
            date = (await TodayAsync(user, ct)).AddDays(1);
        }
        else if (argument.Length > 0)
        {
            if (!TryParseDate(argument, out var parsed))
            {
                await _telegram.SendMessageAsync(
                    chatId,
                    "Send it as <code>/plan</code>, <code>/plan tomorrow</code> or <code>/plan 2026-10-23</code>.",
                    ct: ct);
                return;
            }

            date = parsed;
        }

        await SendPlanDayAsync(chatId, user, date, prefix: null, ct);
    }

    private async Task SendPlanDayAsync(
        long chatId, ResolvedUser user, DateOnly? date, string? prefix, CancellationToken ct)
    {
        var result = await GetPlanDayAsync(user, date, ct);

        if (result.Status == HttpStatusCode.NotFound)
        {
            await _telegram.SendMessageAsync(
                chatId,
                (prefix ?? string.Empty)
                + $"No active plan covers {(date is { } d ? Escape(DayLabel(d)) : "today")}. "
                + $"Set one up at {_options.PublicUrl.TrimEnd('/')}/meal-plan.",
                ct: ct);
            return;
        }

        if (!result.Ok || result.Value is null)
        {
            await _telegram.SendMessageAsync(chatId, (prefix ?? string.Empty) + Escape(ExplainFailure(result)), ct: ct);
            return;
        }

        await _telegram.SendMessageAsync(
            chatId,
            (prefix ?? string.Empty) + PlanDayCard(result.Value),
            PlanDayKeyboard(result.Value, MiniAppLink($"{MiniAppPages.Plan}?planId={result.Value.PlanId:D}&day={result.Value.Date:yyyy-MM-dd}")),
            ct);
    }

    private Task<ApiResult<PlanDayViewDto>> GetPlanDayAsync(ResolvedUser user, DateOnly? date, CancellationToken ct) =>
        _api.SendForResultAsync<PlanDayViewDto>(
            user.UserId,
            HttpMethod.Get,
            date is { } d ? $"api/MealPlans/day?date={d:yyyy-MM-dd}" : "api/MealPlans/day",
            null,
            ct);

    /// <summary>
    /// The user's today, as the API says it: the plan day without a date is dated
    /// in their time zone. Neither the resolve call nor /api/Users/me exposes the
    /// zone to a service caller, so without a plan today UTC is the fallback.
    /// </summary>
    private async Task<DateOnly> TodayAsync(ResolvedUser user, CancellationToken ct)
    {
        var today = await GetPlanDayAsync(user, null, ct);
        return today.Value?.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
    }

    /// <summary>
    /// A Log button carries the entry id alone. The plan it belongs to is found
    /// again on today's or tomorrow's card, the days a button is sent for; an
    /// older button is refused rather than guessed at.
    /// </summary>
    private async Task LogPlanEntryAsync(long chatId, ResolvedUser user, string id, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var entryId))
        {
            await SendStaleCardAsync(chatId, "/plan", ct);
            return;
        }

        var today = await GetPlanDayAsync(user, null, ct);
        var found = FindEntry(today.Value, entryId);

        if (found is null)
        {
            var tomorrow = (today.Value?.Date ?? DateOnly.FromDateTime(DateTime.UtcNow)).AddDays(1);
            found = FindEntry((await GetPlanDayAsync(user, tomorrow, ct)).Value, entryId);
        }

        if (found is null)
        {
            await SendStaleCardAsync(chatId, "/plan", ct);
            return;
        }

        var (view, entry) = found.Value;

        // An empty body: the API dates and types the diary row from the entry.
        var result = await _api.SendForResultAsync<object>(
            user.UserId, HttpMethod.Post, $"api/MealPlans/{view.PlanId:D}/entries/{entry.Id:D}/log", new { }, ct);

        if (!result.Ok)
        {
            await _telegram.SendMessageAsync(chatId, Escape(ExplainFailure(result)), ct: ct);
            return;
        }

        await SendPlanDayAsync(chatId, user, view.Date, $"Logged {Escape(EntryName(entry))}.\n\n", ct);
    }

    private static (PlanDayViewDto View, PlanEntryDto Entry)? FindEntry(PlanDayViewDto? view, Guid entryId)
    {
        var entry = view?.Day.Slots.SelectMany(s => s.Entries).FirstOrDefault(e => e.Id == entryId);
        return view is not null && entry is not null ? (view, entry) : null;
    }

    /// <summary>The day as text: name and date, a block per slot, totals against the rules, then violations and the window.</summary>
    internal static string PlanDayCard(PlanDayViewDto view)
    {
        var text = new StringBuilder();
        var day = view.Day;

        text.Append("<b>").Append(Escape(view.PlanName ?? "Meal plan")).Append("</b> · ").Append(Escape(DayLabel(view.Date)));
        if (day.EntryCount > 0)
        {
            text.Append(" · ").Append(day.LoggedCount).Append(" of ").Append(day.EntryCount).Append(" logged");
        }
        text.AppendLine();

        foreach (var slot in day.Slots.Where(s => s.Entries.Count > 0))
        {
            text.AppendLine().Append("<b>").Append(Escape(SlotLabel(slot.Slot))).AppendLine("</b>");
            foreach (var entry in slot.Entries)
            {
                text.AppendLine(EntryLine(entry));
            }
        }

        if (day.EntryCount == 0)
        {
            text.AppendLine().AppendLine("Nothing is planned for this day.");
        }

        // The words are the domain's; only the bold is ours. The plan's effective
        // targets are shown, because that is what the plan is held to.
        text.AppendLine();
        var targets = view.EffectiveTargets;
        foreach (var amount in NutritionText.Daily(
            day.Totals.Calories, day.Totals.ProteinGrams, day.Totals.CarbsGrams, day.Totals.FatGrams,
            targets.Calories.Value, targets.Protein.Value, targets.MaxCarbs.Value, targets.Fat.Value))
        {
            text.AppendLine(NutritionText.Describe(amount, actual => $"<b>{actual}</b>"));
        }

        if (PlanCostText.Day(view) is { } cost)
        {
            text.AppendLine(Escape(cost));
        }

        if (day.Violations.Count > 0)
        {
            text.AppendLine();
            foreach (var violation in day.Violations)
            {
                text.Append("⚠ ").AppendLine(Escape(violation.Message));
            }
        }

        if (view.Rules is { EatingWindowStart: { } start, EatingWindowEnd: { } end })
        {
            text.AppendLine().Append("Eating window ")
                .Append(start.ToString("HH:mm", CultureInfo.InvariantCulture))
                .Append('–')
                .AppendLine(end.ToString("HH:mm", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    /// <summary>"Oats with milk, 80 g, 300 kcal ✓" - the tick marks an entry already in the diary.</summary>
    internal static string EntryLine(PlanEntryDto entry)
    {
        var parts = new List<string> { Escape(EntryName(entry)) };

        if (entry.Grams is { } grams)
        {
            parts.Add(grams.ToString("0.#", CultureInfo.InvariantCulture) + " g");
        }
        else if (entry.Servings is { } servings)
        {
            parts.Add(servings == 1
                ? "1 serving"
                : servings.ToString("0.#", CultureInfo.InvariantCulture) + " servings");
        }

        if (entry.Nutrition is { } nutrition)
        {
            parts.Add(nutrition.Calories.ToString("0", CultureInfo.InvariantCulture) + " kcal");
        }

        var line = string.Join(", ", parts);
        return entry.Logged ? line + " ✓" : line;
    }

    internal static string EntryName(PlanEntryDto entry) =>
        entry.RecipeTitle ?? entry.FoodName ?? entry.Text ?? "Entry";

    /// <summary>One Log button per entry the API would accept: unlogged, and not a free-text line.</summary>
    internal static bool CanLog(PlanEntryDto entry) =>
        !entry.Logged && !string.Equals(entry.Kind, "Text", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A Log row per loggable entry, then Tomorrow, then Open when the Mini App is reachable.
    /// Each payload is a prefix and one id: well under 64 bytes. Open carries a URL, not callback data.
    /// </summary>
    internal static object PlanDayKeyboard(PlanDayViewDto view, string? openUrl = null)
    {
        var rows = new List<object[]>();

        foreach (var entry in view.Day.Slots.SelectMany(s => s.Entries).Where(CanLog))
        {
            rows.Add([new { text = "Log " + TruncateUtf8(EntryName(entry), 40), callback_data = $"pe:{entry.Id:D}" }]);
        }

        rows.Add([new { text = "Tomorrow", callback_data = $"pd:{view.Date.AddDays(1):yyyy-MM-dd}" }]);
        if (openUrl is not null) rows.Add([OpenButton(openUrl)]);

        return new { inline_keyboard = rows };
    }

    /// <summary>BREAKFAST, the diary's slot name, shown as Breakfast.</summary>
    private static string SlotLabel(string slot) =>
        slot.Length == 0 ? "Meal" : char.ToUpperInvariant(slot[0]) + slot[1..].ToLowerInvariant();

    private static string DayLabel(DateOnly date) => date.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    // ---- Shopping list ----------------------------------------------------

    /// <summary>Items on one shopping page. Ten rows of buttons is what a phone screen shows without scrolling.</summary>
    internal const int ShopPageSize = 10;

    /// <summary>/shop and the More button: the newest list, one page of what is still to buy.</summary>
    private async Task OnShopAsync(long chatId, ResolvedUser user, int page, CancellationToken ct)
    {
        var list = await NewestShoppingListAsync(user, ct);

        if (list is null)
        {
            await _telegram.SendMessageAsync(
                chatId,
                $"You have no shopping list yet. Make one from your plan at {_options.PublicUrl.TrimEnd('/')}/meal-plan.",
                ct: ct);
            return;
        }

        await _telegram.SendMessageAsync(chatId, ShoppingCard(list, page), ShoppingKeyboard(list, page, ShoppingListLink(list)), ct);
    }

    /// <summary>
    /// A toggle button carries the item id alone, so the page it sat on is worked
    /// out from the item's place among the unticked items before it moves.
    /// </summary>
    private async Task ToggleShoppingItemAsync(long chatId, ResolvedUser user, string id, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var itemId))
        {
            await SendStaleCardAsync(chatId, "/shop", ct);
            return;
        }

        var list = await NewestShoppingListAsync(user, ct);
        var index = list is null ? -1 : Unchecked(list).FindIndex(i => i.Id == itemId);

        if (list is null || index < 0)
        {
            await SendStaleCardAsync(chatId, "/shop", ct);
            return;
        }

        var result = await _api.SendForResultAsync<object>(
            user.UserId, HttpMethod.Patch, $"api/ShoppingLists/items/{itemId:D}/toggle",
            new ToggleShoppingListItemRequest(true), ct);

        if (!result.Ok)
        {
            await _telegram.SendMessageAsync(chatId, Escape(ExplainFailure(result)), ct: ct);
            return;
        }

        // Read back rather than ticked locally: the list is the API's, and a
        // household member may have changed it in the meantime.
        var updated = await _api.GetAsAsync<ShoppingListDetailDto>(user.UserId, $"api/ShoppingLists/{list.Id:D}", ct);
        if (updated is null)
        {
            await _telegram.SendMessageAsync(chatId, "Ticked, but I could not read the list back. Send /shop.", ct: ct);
            return;
        }

        var page = index / ShopPageSize;
        await _telegram.SendMessageAsync(chatId, ShoppingCard(updated, page), ShoppingKeyboard(updated, page, ShoppingListLink(updated)), ct);
    }

    /// <summary>The most recently changed list, in full. Null when there is none.</summary>
    private async Task<ShoppingListDetailDto?> NewestShoppingListAsync(ResolvedUser user, CancellationToken ct)
    {
        // The page envelope is the API's own paging record, read as JSON rather
        // than redeclared here; one id is all the bot needs from it.
        var lists = await _api.GetAsAsync<System.Text.Json.JsonElement>(user.UserId, "api/ShoppingLists?pageSize=1", ct);

        if (lists.ValueKind != System.Text.Json.JsonValueKind.Object
            || !lists.TryGetProperty("items", out var items)
            || items.ValueKind != System.Text.Json.JsonValueKind.Array
            || items.GetArrayLength() == 0
            || !items[0].TryGetProperty("id", out var id)
            || !id.TryGetGuid(out var listId))
        {
            return null;
        }

        return await _api.GetAsAsync<ShoppingListDetailDto>(user.UserId, $"api/ShoppingLists/{listId:D}", ct);
    }

    internal static List<ShoppingListItemDto> Unchecked(ShoppingListDetailDto list) =>
        list.Items.Where(i => !i.IsChecked).ToList();

    /// <summary>The page's items, with the page number clamped to what is left, so a card pressed after ticking still lands somewhere.</summary>
    internal static (List<ShoppingListItemDto> Shown, int Page, int Left, bool More) ShopPage(ShoppingListDetailDto list, int page)
    {
        var left = Unchecked(list);
        var lastPage = Math.Max(0, (left.Count - 1) / ShopPageSize);
        page = Math.Clamp(page, 0, lastPage);

        return (left.Skip(page * ShopPageSize).Take(ShopPageSize).ToList(), page, left.Count, page < lastPage);
    }

    /// <summary>Unticked items grouped under their prep session, each with its amount, then "N left of M".</summary>
    internal static string ShoppingCard(ShoppingListDetailDto list, int page)
    {
        var (shown, _, left, _) = ShopPage(list, page);
        var text = new StringBuilder("<b>").Append(Escape(list.Name ?? "Shopping list")).AppendLine("</b>");

        if (shown.Count == 0)
        {
            text.AppendLine().AppendLine("Everything is ticked off.");
        }

        string? session = null;
        foreach (var item in shown)
        {
            if (item.PrepSessionTitle is { Length: > 0 } title && title != session)
            {
                text.AppendLine().Append("<b>").Append(Escape(title)).AppendLine("</b>");
            }
            else if (session is not null && item.PrepSessionTitle is null or "")
            {
                text.AppendLine();
            }
            else if (session is null && text.Length > 0 && shown[0] == item)
            {
                text.AppendLine();
            }

            session = item.PrepSessionTitle is { Length: > 0 } ? item.PrepSessionTitle : null;
            text.Append("• ").Append(ItemLine(item)).AppendLine(Escape(PlanCostText.Item(item, list.Currency)));
        }

        text.AppendLine().Append(left).Append(" left of ").Append(list.TotalItems);
        if (PlanCostText.List(list) is { } total)
        {
            text.AppendLine().Append(Escape(total));
        }
        return text.ToString();
    }

    /// <summary>"500 g Teff", "2 cups Rice", or just the name; "needs review" when the generator was unsure.</summary>
    internal static string ItemLine(ShoppingListItemDto item)
    {
        var amount = item.Grams is { } grams
            ? grams.ToString("0.#", CultureInfo.InvariantCulture) + " g"
            : item.Amount is { } quantity
                ? quantity.ToString("0.##", CultureInfo.InvariantCulture)
                  + (item.Unit is { Length: > 0 } unit ? " " + unit : string.Empty)
                : null;

        var line = amount is null ? Escape(item.ItemName) : amount + " " + Escape(item.ItemName);
        return item.NeedsReview ? line + " (needs review)" : line;
    }

    /// <summary>
    /// One toggle per shown item, then Back and More as the pages allow, then Open when the Mini App
    /// is reachable. More re-resolves the newest list: a list id does not fit beside a page.
    /// </summary>
    internal static object ShoppingKeyboard(ShoppingListDetailDto list, int page, string? openUrl = null)
    {
        var (shown, current, _, more) = ShopPage(list, page);
        var rows = new List<object[]>();

        foreach (var item in shown)
        {
            rows.Add([new { text = "☐ " + TruncateUtf8(item.ItemName, 40), callback_data = $"sh:{item.Id:D}" }]);
        }

        var paging = new List<object>();
        if (current > 0) paging.Add(new { text = "Back", callback_data = $"sp:{current - 1}" });
        if (more) paging.Add(new { text = "More", callback_data = $"sp:{current + 1}" });
        if (paging.Count > 0) rows.Add(paging.ToArray());
        if (openUrl is not null) rows.Add([OpenButton(openUrl)]);

        return new { inline_keyboard = rows };
    }

    private async Task SendStaleCardAsync(long chatId, string command, CancellationToken ct) =>
        await _telegram.SendMessageAsync(
            chatId, $"That card is too old to use. Send <b>{command}</b> for a fresh one.", ct: ct);

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

    private async Task SendUpgradePromptAsync(long chatId, CancellationToken ct) =>
        await _telegram.SendMessageAsync(
            chatId,
            "The Telegram bot is part of Mizan Pro.\n\n"
            + $"Upgrade at {_options.PublicUrl.TrimEnd('/')}/billing and this chat works right away. "
            + "Logging on the website stays free.\n\n"
            + "<b>/unlink</b> - disconnect this chat",
            ct: ct);

    private static bool IsAlwaysAllowed(string text)
    {
        var command = text.Split(' ', 2)[0].Split('@', 2)[0].ToLowerInvariant();
        return command is "/unlink" or "/help";
    }

    private static string Greeting(string? name) =>
        (name is { Length: > 0 } ? $"Connected. Hello {Escape(name)}.\n\n" : "Connected.\n\n") + HelpText();

    private static string HelpText() =>
        "Send me a photo of a meal and I will estimate it. You choose to log it, split it with the people you ate with, or discard it.\n"
        + "Ask me anything the way you would on the website; it is the same conversation.\n\n"
        + "<b>/today</b> - totals against your targets\n"
        + "<b>/weight 82.4</b> - log a weigh-in\n"
        + "<b>/plan</b> - today's meal plan, with a Log button per entry (<b>/plan tomorrow</b> works too)\n"
        + "<b>/shop</b> - your shopping list, tick items off as you go\n"
        + "<b>/planme high protein, cheap</b> - draft a week of meals to confirm\n"
        + "<b>/notifications</b> - get the app's notifications here too (<b>on</b> or <b>off</b>)\n"
        + "<b>/unlink</b> - disconnect this chat";

    private static string AnalysisCard(FoodAnalysisResult analysis)
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

    /// <summary>
    /// Turns an API failure into something a person can act on. A quota or
    /// upgrade wall is the common one and deserves better than "failed".
    /// </summary>
    private string ExplainFailure<T>(ApiResult<T> result) => result.Status switch
    {
        // The API would not act for this account: suspended, deleted, or the bot's own key is wrong.
        // "Unauthorized" tells the person nothing they can do.
        HttpStatusCode.Unauthorized =>
            $"I could not act for your Mizan account just now. Check that you can sign in at {_options.PublicUrl.TrimEnd('/')}, "
            + $"then reconnect this chat at {SettingsUrl()} if it keeps happening.",
        HttpStatusCode.Forbidden => result.Problem
            ?? $"That needs a Pro plan. Manage it at {_options.PublicUrl.TrimEnd('/')}/billing.",
        HttpStatusCode.TooManyRequests => result.Problem
            ?? "You have used today's assistant allowance. It resets tomorrow.",
        _ => result.Problem ?? "That did not work. Try again in a moment.",
    };

    private string SettingsUrl() => $"{_options.PublicUrl.TrimEnd('/')}/profile/settings/telegram";

    /// <summary>
    /// One button that opens the Mini App at <paramref name="path"/> under the
    /// configured base. Telegram accepts <c>web_app</c> URLs over HTTPS only and
    /// refuses the whole message otherwise, so a plain-HTTP development setup
    /// gets the text without the button rather than no text at all.
    /// </summary>
    internal InlineKeyboardMarkup? MiniAppKeyboard(string text, string path = MiniAppPages.Home) =>
        MiniAppLink(path) is { } url ? InlineKeyboardMarkup.Column(InlineKeyboardButton.OpenWebApp(text, url)) : null;

    /// <summary>The Mini App URL for <paramref name="path"/>, or null when it is not HTTPS and Telegram would refuse it.</summary>
    internal string? MiniAppLink(string path = "") => _options.MiniAppButtonUrl(path);

    private string? ShoppingListLink(ShoppingListDetailDto list) => MiniAppLink($"{MiniAppPages.ShoppingList}?listId={list.Id:D}");

    /// <summary>A <c>web_app</c> button in its own row, the shape the /help button has.</summary>
    private static object OpenButton(string url) => InlineKeyboardButton.OpenWebApp("Open", url);

    internal string MiniAppUrl(string path = "") => _options.MiniAppPageUrl(path);

    /// <summary>
    /// Messages go out as HTML, and every one of them contains something a
    /// user or a model wrote. Escaped, or a reply containing a stray angle
    /// bracket fails to send at all.
    /// </summary>
    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
