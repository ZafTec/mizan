using Mizan.Contracts.Notifications;
using Mizan.Contracts.Telegram;
using Mizan.Telegram.Backend;

namespace Mizan.Telegram.Bot;

/// <summary>
/// Reminders in the chat (docs/TELEGRAM.md#reminders). <c>/remind</c> asks what, which days and what
/// time with buttons, then sends the same <c>POST /api/Reminders</c> the website sends; <c>/reminders</c>
/// lists them with a Stop button each. A reminder's own card carries Done, Snooze 30 min and Skip today,
/// which call the reminder's endpoints. The bot keeps no state and decides nothing: each button carries
/// its choices, a custom reminder's words ride in the message text, and every rule is the API's.
/// Each step is also reported to the person's assistant conversation as app context
/// (docs/AI.md#app-context), so words typed instead of a button are read against the open question.
/// </summary>
public sealed class RemindHandler
{
    public const string Command = "/remind";
    public const string ListCommand = "/reminders";

    /// <summary>Callback prefix of the guided flow: <c>rm:k:{kind}</c>, <c>rm:d:{kind}:{days}</c>, <c>rm:t:{kind}:{days}:{HHmm}</c>.</summary>
    public const string Prefix = "rm:";

    /// <summary>The line a custom reminder's words are carried on, read back when a button is pressed.</summary>
    public const string CustomLead = "Remind you to: ";

    private static readonly (string Kind, string Label)[] Kinds =
    [
        ("workout", "Workout"),
        ("measurement", "Measurements"),
        ("meal_log", "Log a meal"),
    ];

    private static readonly (string Key, string Label)[] DayPresets =
    [
        ("daily", "Every day"),
        ("wk", "Weekdays"),
        ("we", "Weekends"),
        ("2d", "Every other day"),
    ];

    private static readonly string[] Times = ["07:00", "08:00", "12:00", "18:00", "20:00", "21:00"];

    /// <summary>How long a step waits for typed words to count as its answer.</summary>
    public const int StepHoldMinutes = ChatContext.QuestionHoldMinutes;

    private readonly TelegramClient _telegram;
    private readonly MizanApiClient _api;
    private readonly Func<ApiResult<object>, string> _explain;
    private readonly ChatContext _context;

    public RemindHandler(TelegramClient telegram, MizanApiClient api, ChatContext context, Func<ApiResult<object>, string> explain)
    {
        _telegram = telegram;
        _api = api;
        _explain = explain;
        _context = context;
    }

    public static bool IsCallback(string data) =>
        data.StartsWith(Prefix, StringComparison.Ordinal) || data.StartsWith(TelegramReminderCallbacks.Prefix, StringComparison.Ordinal);

    public async Task HandleCommandAsync(long chatId, ResolvedUser user, string argument, CancellationToken ct)
    {
        if (argument.Length > 0)
        {
            await _telegram.SendMessageAsync(chatId, $"{CustomLead}<b>{Escape(argument)}</b>\nWhich days?", DaysKeyboard("custom"), ct);
            await _context.AskedAsync(user, Step(KindChoice("custom", argument), "Which days?", DayPresets.Select(d => d.Label)), ct);
            return;
        }

        await _telegram.SendMessageAsync(
            chatId,
            "What should I remind you about? Or send <code>/remind</code> and the words, such as <code>/remind drink water</code>.",
            InlineKeyboardMarkup.Column(Kinds.Select(k => InlineKeyboardButton.Callback(k.Label, $"{Prefix}k:{k.Kind}")).ToArray()),
            ct);
    }

    public async Task HandleListAsync(long chatId, ResolvedUser user, CancellationToken ct)
    {
        var result = await _api.SendForResultAsync<ReminderListResult>(user.UserId, HttpMethod.Get, "api/Reminders", null, ct);
        if (result is not { Ok: true, Value: { } list })
        {
            await _telegram.SendMessageAsync(chatId, Escape(_explain(Untyped(result))), ct: ct);
            return;
        }

        var (text, keyboard) = ListCard(list);
        await _telegram.SendMessageAsync(chatId, text, keyboard, ct);
        await _context.ReportAsync(user, ChatContext.Showed("The /reminders command", text), ct);
    }

    /// <summary>Each reminder on a line with the API's summary and next time; Stop buttons for those on.</summary>
    internal static (string Text, InlineKeyboardMarkup? Keyboard) ListCard(ReminderListResult list)
    {
        if (list.Items.Count == 0)
            return ("You have no reminders. Send <code>/remind</code> to set one.", null);

        var lines = new List<string> { $"<b>Your reminders</b> ({list.ActiveCount} of {list.ActiveLimit} on)", "" };
        lines.AddRange(list.Items.Select(r =>
            $"{Escape(r.Title)}: {Escape(r.Summary)}" + (r.Enabled ? r.NextRunText is { } next ? $", next {Escape(next)}" : "" : " <i>(off)</i>")));
        var stops = list.Items.Where(r => r.Enabled)
            .Select(r => InlineKeyboardButton.Callback($"Stop: {Shorten(r.Title)}", TelegramReminderCallbacks.Data(TelegramReminderCallbacks.Stop, r.Id)))
            .ToArray();
        return (string.Join('\n', lines), stops.Length == 0 ? null : InlineKeyboardMarkup.Column(stops));
    }

    public async Task HandleCallbackAsync(long chatId, ResolvedUser user, string data, string? messageText, CancellationToken ct)
    {
        if (TelegramReminderCallbacks.TryParse(data, out var action, out var reminderId))
        {
            await ActAsync(chatId, user, action, reminderId, ct);
            return;
        }

        var parts = data[Prefix.Length..].Split(':');
        var title = CustomTitle(messageText);
        switch (parts)
        {
            case ["k", var kind]:
                await _telegram.SendMessageAsync(chatId, "Which days?", DaysKeyboard(kind), ct);
                await _context.AskedAsync(user, Step(KindChoice(kind, title), "Which days?", DayPresets.Select(d => d.Label)), ct);
                return;

            case ["d", var kind, var days] when kind != "custom" || title is not null:
                await _telegram.SendMessageAsync(
                    chatId,
                    (title is null ? "" : $"{CustomLead}<b>{Escape(title)}</b>\n") + "What time?",
                    TimesKeyboard(kind, days),
                    ct);
                await _context.AskedAsync(user, Step($"{KindChoice(kind, title)}; days: {DaysChoice(days)}", "What time?", Times), ct);
                return;

            case ["t", var kind, var days, var hhmm] when (kind != "custom" || title is not null) && Schedule(days, hhmm) is { } schedule:
                await CreateAsync(chatId, user, new CreateReminderRequest(kind, Title: title, Schedule: schedule), ct);
                return;

            default:
                await _telegram.SendMessageAsync(chatId, "That card is too old to use. Send <code>/remind</code> again.", ct: ct);
                return;
        }
    }

    private async Task CreateAsync(long chatId, ResolvedUser user, CreateReminderRequest request, CancellationToken ct)
    {
        var result = await _api.SendForResultAsync<ReminderDto>(user.UserId, HttpMethod.Post, "api/Reminders", request, ct);
        if (result is not { Ok: true, Value: { } reminder })
        {
            var reason = _explain(Untyped(result));
            await _telegram.SendMessageAsync(chatId, Escape(reason), ct: ct);
            await _context.ReportAsync(user, $"The /remind buttons ended without a reminder: {reason}", ct);
            return;
        }

        await _context.ReportAsync(user, $"The /remind buttons created the reminder \"{reminder.Title}\": {reminder.Summary}. That flow is finished.", ct);

        var first = reminder.NextRunText is { } next ? $" First one {Escape(next)}." : "";
        await _telegram.SendMessageAsync(
            chatId,
            $"Set: <b>{Escape(reminder.Title)}</b>, {Escape(reminder.Summary)}.{first}\nSee them all with <code>/reminders</code>.",
            ct: ct);
    }

    private async Task ActAsync(long chatId, ResolvedUser user, string action, Guid id, CancellationToken ct)
    {
        var button = ButtonLabel(action);
        if (action == TelegramReminderCallbacks.Stop)
        {
            var stopped = await _api.SendForResultAsync<ReminderDto>(user.UserId, HttpMethod.Put, $"api/Reminders/{id}", new UpdateReminderRequest(Enabled: false), ct);
            if (stopped is { Ok: true, Value: { } off })
            {
                await _telegram.SendMessageAsync(chatId, $"Stopped <b>{Escape(off.Title)}</b>. Turn it back on in Settings, Reminders.", ct: ct);
                await _context.ReportAsync(user, $"The person pressed {button} on the reminder \"{off.Title}\" in Telegram: it is now off.", ct);
                return;
            }

            var refused = _explain(Untyped(stopped));
            await _telegram.SendMessageAsync(chatId, Escape(refused), ct: ct);
            await _context.ReportAsync(user, $"The person pressed {button} on a reminder in Telegram and it was refused: {refused}", ct);
            return;
        }

        var path = $"api/Reminders/{id}/{action}";
        var result = action == TelegramReminderCallbacks.Snooze
            ? await _api.SendForResultAsync<ReminderActionResult>(user.UserId, HttpMethod.Post, path, new SnoozeReminderRequest(), ct)
            : await _api.SendForResultAsync<ReminderActionResult>(user.UserId, HttpMethod.Post, path, null, ct);
        if (result is { Ok: true, Value: { } done })
        {
            await _telegram.SendMessageAsync(chatId, Escape(done.Message), ct: ct);
            await _context.ReportAsync(user, $"The person pressed {button} on the reminder \"{done.Reminder.Title}\" in Telegram. The app answered: {done.Message}", ct);
            return;
        }

        var reason = _explain(Untyped(result));
        await _telegram.SendMessageAsync(chatId, Escape(reason), ct: ct);
        await _context.ReportAsync(user, $"The person pressed {button} on a reminder in Telegram and it was refused: {reason}", ct);
    }

    private static string ButtonLabel(string action) => action switch
    {
        TelegramReminderCallbacks.Done => "Done",
        TelegramReminderCallbacks.Snooze => "Snooze",
        TelegramReminderCallbacks.Skip => "Skip today",
        TelegramReminderCallbacks.Stop => "Stop",
        _ => action,
    };

    internal static string Step(string chosen, string question, IEnumerable<string> offered) =>
        $"The person is setting up a reminder with the /remind buttons. Chosen so far: {chosen}. "
        + $"The chat is now asking \"{question}\" with buttons: {string.Join(", ", offered)}.";

    private static string KindChoice(string kind, string? title) =>
        kind == "custom"
            ? $"a custom reminder (kind custom) titled \"{title}\""
            : $"{Kinds.FirstOrDefault(k => k.Kind == kind).Label ?? kind} (kind {kind})";

    private static string DaysChoice(string days) =>
        DayPresets.FirstOrDefault(d => d.Key == days).Label ?? days;

    private static InlineKeyboardMarkup DaysKeyboard(string kind) =>
        InlineKeyboardMarkup.Column(DayPresets.Select(d => InlineKeyboardButton.Callback(d.Label, $"{Prefix}d:{kind}:{d.Key}")).ToArray());

    private static InlineKeyboardMarkup TimesKeyboard(string kind, string days) => new()
    {
        InlineKeyboard = Times.Chunk(3)
            .Select(row => (IReadOnlyList<InlineKeyboardButton>)row.Select(t => InlineKeyboardButton.Callback(t, $"{Prefix}t:{kind}:{days}:{t.Replace(":", "")}")).ToList())
            .ToList(),
    };

    /// <summary>The button's choice as the API's schedule. Whether it is allowed is the API's to say.</summary>
    internal static ReminderSchedule? Schedule(string days, string hhmm)
    {
        if (hhmm.Length != 4) return null;
        var time = $"{hhmm[..2]}:{hhmm[2..]}";
        return days switch
        {
            "daily" => new ReminderSchedule("daily", time),
            "wk" => new ReminderSchedule("days", time, ["mon", "tue", "wed", "thu", "fri"]),
            "we" => new ReminderSchedule("days", time, ["sat", "sun"]),
            "2d" => new ReminderSchedule("every", time, EveryDays: 2),
            _ => null,
        };
    }

    /// <summary>A custom reminder's words, from the first line of the message the button was on.</summary>
    internal static string? CustomTitle(string? messageText)
    {
        var first = messageText?.Split('\n')[0];
        return first is not null && first.StartsWith(CustomLead, StringComparison.Ordinal) && first.Length > CustomLead.Length
            ? first[CustomLead.Length..].Trim()
            : null;
    }

    private static ApiResult<object> Untyped<T>(ApiResult<T> result) => new(result.Ok, result.Value, result.Problem, result.Status);

    private static string Shorten(string title) => title.Length <= 24 ? title : title[..23] + "…";

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
