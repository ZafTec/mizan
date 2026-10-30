using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Planning;
using Mizan.Telegram.Backend;
using Mizan.Domain.Pricing;

namespace Mizan.Telegram.Bot;

/// <summary>
/// <c>/planme &lt;words&gt;</c>: the assistant drafts a week from tomorrow through the same
/// <c>POST /api/MealPlans/generate</c> the website uses, and the reply is the draft's summary with
/// <b>Confirm</b> and <b>Discard</b>. Confirm accepts the draft (it becomes the active plan);
/// Discard declines it (it is archived). The bot computes nothing: the cost, the violations and
/// the lines to review are the API's (docs/TELEGRAM.md).
/// </summary>
public sealed class PlanMeHandler
{
    public const string Command = "/planme";

    /// <summary>Callback prefixes. With a plan id the data is 42 bytes, under Telegram's 64.</summary>
    public const string ConfirmPrefix = "pm:ok:";
    public const string DiscardPrefix = "pm:no:";

    public const int PlanDays = 7;

    private readonly TelegramClient _telegram;
    private readonly MizanApiClient _api;
    private readonly TelegramBotOptions _options;
    private readonly ChatContext _context;

    public PlanMeHandler(TelegramClient telegram, MizanApiClient api, ChatContext context, TelegramBotOptions options)
    {
        _telegram = telegram;
        _api = api;
        _options = options;
        _context = context;
    }

    public PlanMeHandler(TelegramClient telegram, MizanApiClient api, ChatContext context, IOptions<TelegramBotOptions> options)
        : this(telegram, api, context, options.Value)
    {
    }

    public static bool IsCallback(string data) =>
        data.StartsWith(ConfirmPrefix, StringComparison.Ordinal) || data.StartsWith(DiscardPrefix, StringComparison.Ordinal);

    public async Task HandleCommandAsync(long chatId, ResolvedUser user, string argument, CancellationToken ct)
    {
        if (argument.Length == 0)
        {
            await _telegram.SendMessageAsync(
                chatId,
                "Tell me what you want, e.g. <code>/planme high protein, cheap, no pork</code>. "
                + "I will draft a week from tomorrow for you to confirm.",
                ct: ct);
            return;
        }

        await _telegram.SendTypingAsync(chatId, ct);

        var request = new GenerateMealPlanRequest
        {
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            DurationDays = PlanDays,
            Preference = argument,
        };
        var result = await _api.SendForResultAsync<GenerateMealPlanResult>(
            user.UserId, HttpMethod.Post, "api/MealPlans/generate", request, ct);

        if (!result.Ok || result.Value is not { Drafts.Count: > 0 } generated)
        {
            var reason = Explain(result.Status, result.Problem);
            await _telegram.SendMessageAsync(chatId, Escape(reason), ct: ct);
            await _context.ReportAsync(user, $"The /planme command asked for a week's plan (\"{argument}\") and got none: {reason}", ct);
            return;
        }

        var cards = new List<string>();
        foreach (var draft in generated.Drafts)
        {
            var summary = Summary(draft);
            cards.Add(ChatContext.Plain(summary));
            await _telegram.SendMessageAsync(chatId, summary, Keyboard(draft.Plan.Id), ct);
        }

        await _context.AskedAsync(
            user,
            $"The /planme command drafted a meal plan for \"{argument}\" and showed the person: {string.Join(" | ", cards)}. "
            + "The chat is now asking whether to keep it, with buttons: Confirm, Discard.",
            ct);
    }

    public async Task HandleCallbackAsync(long chatId, ResolvedUser user, string data, CancellationToken ct)
    {
        var confirm = data.StartsWith(ConfirmPrefix, StringComparison.Ordinal);
        var raw = data[(confirm ? ConfirmPrefix : DiscardPrefix).Length..];
        if (!Guid.TryParse(raw, out var planId))
        {
            await _telegram.SendMessageAsync(chatId, "That card is too old to use. Ask for a new plan.", ct: ct);
            return;
        }

        var result = await _api.SendForResultAsync<object>(
            user.UserId, HttpMethod.Post, $"api/MealPlans/{planId}/{(confirm ? "accept" : "decline")}", null, ct);

        var button = confirm ? "Confirm" : "Discard";
        if (!result.Ok)
        {
            var reason = Explain(result.Status, result.Problem);
            await _telegram.SendMessageAsync(chatId, Escape(reason), ct: ct);
            await _context.ReportAsync(user, $"The person pressed {button} on the /planme draft {planId} and it was refused: {reason}", ct);
            return;
        }

        await _telegram.SendMessageAsync(
            chatId,
            confirm
                ? $"Done. It is your plan now: {_options.PublicUrl.TrimEnd('/')}/meal-plan/{planId}"
                : "Discarded. The draft is archived and nothing changed in your plans.",
            ct: ct);
        await _context.ReportAsync(
            user,
            confirm
                ? $"The person pressed Confirm on the /planme draft {planId}: it is now their active meal plan. That flow is finished."
                : $"The person pressed Discard on the /planme draft {planId}: it is archived and their plans are unchanged. That flow is finished.",
            ct);
    }

    /// <summary>The draft in a few lines: name and dates, size, weekly cost against the budget, then what breaks a rule and what needs a look.</summary>
    internal static string Summary(GeneratedPlanDto draft)
    {
        var plan = draft.Plan;
        var text = new StringBuilder();
        text.Append("<b>").Append(Escape(plan.Name ?? "Meal plan")).Append("</b> (draft)\n");
        if (plan.StartDate is { } start && plan.EndDate is { } end)
            text.Append(CultureInfo.InvariantCulture, $"{start:ddd d MMM} to {end:ddd d MMM}, {plan.EntryCount} meals\n");

        var cost = draft.Cost;
        var currency = plan.Rules.Currency ?? cost.Currency;
        text.Append("About <b>").Append(Escape(Money.Format(cost.WeeklyTotal, cost.Currency))).Append("</b> a week");
        if (draft.BudgetCeiling is { } budget) text.Append(" (budget ").Append(Escape(Money.Format(budget, currency))).Append(')');
        if (plan.Rules.DailyBudget is { } daily) text.Append(", at most ").Append(Escape(Money.Format(daily, currency))).Append(" a day");
        if (cost.TotalItems > 0) text.Append(CultureInfo.InvariantCulture, $", {cost.PricedItems} of {cost.TotalItems} items priced");
        text.Append('\n');

        foreach (var day in plan.Days.Take(3))
        {
            var meals = day.Slots.SelectMany(s => s.Entries)
                .Select(e => e.RecipeTitle ?? e.FoodName ?? e.Text ?? "?")
                .ToList();
            if (meals.Count == 0) continue;
            text.Append(CultureInfo.InvariantCulture, $"Day {day.DayIndex + 1}: ").Append(Escape(string.Join(", ", meals))).Append('\n');
        }
        if (plan.Days.Count > 3) text.Append("…\n");

        if (draft.Unmet.Count > 0)
        {
            text.Append("\n<b>Not met</b>\n");
            foreach (var unmet in draft.Unmet.Take(5)) text.Append("• ").Append(Escape(unmet)).Append('\n');
            if (draft.Unmet.Count > 5) text.Append(CultureInfo.InvariantCulture, $"and {draft.Unmet.Count - 5} more\n");
        }
        if (draft.Unresolved.Count > 0)
            text.Append(CultureInfo.InvariantCulture, $"\n{draft.Unresolved.Count} line(s) to review in the app.\n");

        return text.ToString();
    }

    internal static object Keyboard(Guid planId) => new
    {
        inline_keyboard = new[]
        {
            new object[]
            {
                new { text = "Confirm", callback_data = ConfirmPrefix + planId },
                new { text = "Discard", callback_data = DiscardPrefix + planId },
            },
        },
    };

    private string Explain(HttpStatusCode status, string? problem) => status switch
    {
        HttpStatusCode.TooManyRequests => problem ?? "You have used today's planning allowance. It resets tomorrow.",
        HttpStatusCode.ServiceUnavailable => problem ?? "The assistant could not draft a plan right now. Try again in a moment.",
        HttpStatusCode.Forbidden => problem ?? $"That needs a Pro plan. Manage it at {_options.PublicUrl.TrimEnd('/')}/billing.",
        _ => problem ?? "That did not work. Try again in a moment.",
    };

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
