extern alias telegram;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using telegram::Mizan.Telegram;
using telegram::Mizan.Telegram.Backend;
using telegram::Mizan.Telegram.Bot;

namespace Mizan.Tests.Application;

/// <summary>
/// Everything outside the bot: Telegram's API on one side, Mizan's on the other, both answered from here. Tests
/// script the API's answers and read what the bot sent, so they check behaviour at the two wires and nothing in
/// between.
/// </summary>
internal sealed class TelegramFakeWorld : HttpMessageHandler
{
    public static readonly Guid User = Guid.Parse("11111111-1111-4111-8111-111111111111");

    private readonly List<(string Url, string Body)> _telegramSends = [];

    // ---- what the API will say ----

    public bool Linked { get; set; } = true;
    public bool IsPro { get; set; } = true;
    public string? LinkProblem { get; set; }
    public string FoodName { get; set; } = "Pasta";
    public HttpStatusCode MealsStatus { get; set; } = HttpStatusCode.OK;
    public string DailyJson { get; set; } = """
        {"date":"2026-10-22","totalCalories":1860,"totalProtein":120,"totalCarbs":200,"totalFat":60,
         "targetCalories":2000,"targetProtein":150,"targetCarbs":null,"targetFat":null,"mealBreakdown":[]}
        """;
    public string ThreadsJson { get; set; } = "[]";
    public string ChatReply { get; set; } = "Hello.";
    public string PerformedJson { get; set; } = "[]";
    public HttpStatusCode GenerateStatus { get; set; } = HttpStatusCode.OK;
    public string GenerateJson { get; set; } = "{}";

    // ---- the meal plan the API holds ----

    /// <summary>The user's today as the API sees it; the plan starts here and runs a week.</summary>
    public DateOnly PlanToday { get; set; } = new(2026, 10, 22);
    public bool HasPlan { get; set; } = true;
    public Guid PlanId { get; } = Guid.Parse("55555555-5555-4555-8555-555555555555");
    public string PlanName { get; set; } = "Cut week";
    public HttpStatusCode LogEntryStatus { get; set; } = HttpStatusCode.OK;
    public List<FakePlanEntry> PlanEntries { get; } =
    [
        new(Guid.Parse("a1111111-1111-4111-8111-111111111111"), "BREAKFAST", "Food", "Oats with milk", 80, null, 300, Logged: true),
        new(Guid.Parse("a2222222-2222-4222-8222-222222222222"), "LUNCH", "Recipe", "Shiro & injera", null, 1.5m, 650, Logged: false),
        new(Guid.Parse("a3333333-3333-4333-8333-333333333333"), "DINNER", "Food", "Chicken breast", 200, null, 330, Logged: false),
        new(Guid.Parse("a4444444-4444-4444-8444-444444444444"), "SNACK", "Text", "Whatever is in the fridge", null, null, null, Logged: false),
    ];

    // ---- the shopping list the API holds ----

    public bool HasShoppingList { get; set; } = true;
    public Guid ShoppingListId { get; } = Guid.Parse("66666666-6666-4666-8666-666666666666");
    public List<FakeShopItem> ShopItems { get; } = Enumerable.Range(1, 12).Select(n => new FakeShopItem(
        Guid.Parse($"b{n:D7}-0000-4000-8000-000000000000"),
        n <= 4 ? $"Batch item {n}" : $"Item {n}",
        n <= 4 ? "Sunday batch" : null,
        Grams: n % 3 == 0 ? 500 : null,
        Amount: n % 3 == 1 ? 2 : null,
        Unit: n % 3 == 1 ? "pcs" : null,
        NeedsReview: n == 2,
        Checked: false)).ToList();

    /// <summary>What POST api/Telegram/sessions answers when the chat is linked and Pro. 403 plays a banned account.</summary>
    public HttpStatusCode SessionStatus { get; set; } = HttpStatusCode.OK;
    public const string SessionToken = "sess-0123456789abcdef";
    public static readonly DateTime SessionExpiresAt = new(2026, 10, 23, 10, 25, 0, DateTimeKind.Utc);

    /// <summary>The bot service's own clock, for the initData window. Matches <see cref="SignedInitData.Now"/>.</summary>
    public DateTimeOffset Now { get; set; } = SignedInitData.Now;
    public string EnvironmentName { get; set; } = "Production";

    // ---- what the bot asked for ----

    public List<JsonElement> MealsPosted { get; } = [];
    public List<JsonElement> ChatPosted { get; } = [];
    public List<JsonElement> LinkPosted { get; } = [];
    public List<string> PlanDaysRequested { get; } = [];
    public List<(string Url, JsonElement Body)> PlanLogsPosted { get; } = [];
    public List<(string Url, JsonElement Body)> TogglesPatched { get; } = [];
    public List<JsonElement> PlansGenerated { get; } = [];
    public List<string> PlanDecisions { get; } = [];
    public List<JsonElement> SessionsPosted { get; } = [];

    public JsonElement LastSend => JsonDocument.Parse(_telegramSends.Last(s => s.Url.EndsWith("/sendMessage")).Body).RootElement.Clone();

    public string LastText => LastSend.GetProperty("text").GetString()!;

    public List<(string Text, string Data)> LastKeyboard() =>
        LastSend.GetProperty("reply_markup").GetProperty("inline_keyboard").EnumerateArray().SelectMany(row => row.EnumerateArray())
            .Select(b => (b.GetProperty("text").GetString()!, b.GetProperty("callback_data").GetString()!)).ToList();

    public IOptions<TelegramBotOptions> BotOptions { get; } = Options.Create(new TelegramBotOptions
    {
        BotToken = SignedInitData.Token,
        ServiceApiKey = "key",
        ApiUrl = "http://api.test",
        PublicUrl = "https://mizan.test",
    });

    public UpdateHandler Handler()
    {
        var options = BotOptions;
        return new UpdateHandler(
            new TelegramClient(new HttpClient(this), options, NullLogger<TelegramClient>.Instance),
            new MizanApiClient(new HttpClient(this), options, NullLogger<MizanApiClient>.Instance),
            options,
            NullLogger<UpdateHandler>.Instance);
    }

    public MiniAppSessionEndpoint MiniAppEndpoint() =>
        new(
            new MizanApiClient(new HttpClient(this), BotOptions, NullLogger<MizanApiClient>.Instance),
            BotOptions,
            new FakeEnvironment(EnvironmentName),
            new FixedClock(this),
            NullLogger<MiniAppSessionEndpoint>.Instance);

    /// <summary>
    /// Runs the endpoint against a bare HttpContext carrying <paramref name="body"/>, and returns what the page would
    /// see: the status, the parsed body and the Set-Cookie header, if any.
    /// </summary>
    public async Task<(int Status, JsonElement Body, string? SetCookie)> PostMiniAppSessionAsync(string body, string contentType = "application/json")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/telegram/miniapp/session";
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.Headers.UserAgent = "Telegram-Android/11.0";
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");
        context.Response.Body = new MemoryStream();

        await MiniAppEndpoint().HandleAsync(context, CancellationToken.None);

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var parsed = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        var cookie = context.Response.Headers.SetCookie.Count == 0 ? null : context.Response.Headers.SetCookie.ToString();
        return (context.Response.StatusCode, parsed, cookie);
    }

    private sealed class FixedClock(TelegramFakeWorld world) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => world.Now;
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Mizan.Telegram";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    // ---- updates a person can cause ----

    public static Update Text(string text) => new()
    {
        UpdateId = 1,
        Message = new Message
        {
            MessageId = 10,
            From = new TelegramUser { Id = 42, FirstName = "T", Username = "tester" },
            Chat = new Chat { Id = 99, Type = "private" },
            Text = text,
        },
    };

    public static Update Photo() => new()
    {
        UpdateId = 1,
        Message = new Message
        {
            MessageId = 10,
            From = new TelegramUser { Id = 42, FirstName = "T" },
            Chat = new Chat { Id = 99, Type = "private" },
            Photo = [new PhotoSize { FileId = "file", Width = 800, Height = 600 }],
        },
    };

    public static Update Press(string data) => new()
    {
        UpdateId = 2,
        CallbackQuery = new CallbackQuery
        {
            Id = "cb",
            From = new TelegramUser { Id = 42 },
            Data = data,
            Message = new Message { MessageId = 11, Chat = new Chat { Id = 99, Type = "private" } },
        },
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

        static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        if (url.StartsWith("https://api.telegram.org/file/"))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        if (url.StartsWith("https://api.telegram.org/"))
        {
            if (url.EndsWith("/getFile")) return Json("""{"ok":true,"result":{"file_path":"photos/a.jpg","file_size":3}}""");
            if (url.EndsWith("/sendMessage")) _telegramSends.Add((url, body));
            return Json("""{"ok":true,"result":{}}""");
        }

        if (url.EndsWith("api/Telegram/sessions"))
        {
            SessionsPosted.Add(JsonDocument.Parse(body).RootElement.Clone());
            if (!Linked) return Json("""{"errorCode":"not_found","error":"This Telegram account is not linked to a Mizan account."}""", HttpStatusCode.NotFound);
            if (!IsPro) return Json("""{"errorCode":"upgrade_required","error":"The Telegram bot is part of Pro."}""", HttpStatusCode.PaymentRequired);
            if (SessionStatus != HttpStatusCode.OK) return Json("""{"errorCode":"forbidden","error":"This account is suspended."}""", SessionStatus);
            return Json($$"""{"token":"{{SessionToken}}","expiresAt":"{{SessionExpiresAt:O}}","userId":"{{User}}"}""");
        }

        if (url.Contains("api/Telegram/resolve/"))
        {
            return Linked
                ? Json($$"""{"userId":"{{User}}","name":"Tester","linkedAt":"2026-10-01T00:00:00Z","isPro":{{IsPro.ToString().ToLowerInvariant()}}}""")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
        if (url.EndsWith("api/Telegram/resolve"))
        {
            LinkPosted.Add(JsonDocument.Parse(body).RootElement.Clone());
            return LinkProblem is null
                ? Json($$"""{"userId":"{{User}}","name":"Tester"}""")
                : Json($$"""{"errorCode":"invalid_code","error":"{{LinkProblem}}"}""", HttpStatusCode.BadRequest);
        }
        if (url.Contains("api/Nutrition/daily")) return Json(DailyJson);
        if (url.Contains("api/Nutrition/ai/analyze-image"))
            return Json($$"""
                {"foods":[{"name":"{{FoodName}}","portionGrams":300,"calories":600,"protein":30,"carbs":60,"fat":20}],
                 "totalCalories":600,"confidence":0.8,"note":null}
                """);
        if (url.Contains("api/Ai/threads")) return Json(ThreadsJson);
        if (url.Contains("api/Ai/chat"))
        {
            ChatPosted.Add(JsonDocument.Parse(body).RootElement.Clone());
            var reply = JsonSerializer.Serialize(ChatReply);
            return Json($$"""
                {"threadId":"22222222-2222-4222-8222-222222222222","title":"Chat",
                 "reply":{"id":"33333333-3333-4333-8333-333333333333","fromUser":false,"content":{{reply}},"createdAt":"2026-10-22T10:00:00Z"},
                 "performed":{{PerformedJson}}}
                """);
        }
        if (url.Contains("api/MealPlans/generate"))
        {
            PlansGenerated.Add(JsonDocument.Parse(body).RootElement.Clone());
            return GenerateStatus == HttpStatusCode.OK
                ? Json(GenerateJson)
                : Json("""{"errorCode":"ai_quota_exceeded","error":"You have used your AI allowance for now."}""", GenerateStatus);
        }
        if (url.Contains("api/MealPlans/") && (url.EndsWith("/accept") || url.EndsWith("/decline")))
        {
            PlanDecisions.Add(url[(url.IndexOf("api/MealPlans/", StringComparison.Ordinal) + "api/MealPlans/".Length)..]);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
        if (url.Contains("api/Meals"))
        {
            MealsPosted.Add(JsonDocument.Parse(body).RootElement.Clone());
            return MealsStatus == HttpStatusCode.OK ? Json("{}") : Json("""{"errorCode":"bad_request","error":"No."}""", MealsStatus);
        }

        if (url.Contains("api/MealPlans/day"))
        {
            var query = request.RequestUri!.Query;
            PlanDaysRequested.Add(query);
            var date = query.Length == 0 ? PlanToday : DateOnly.ParseExact(query["?date=".Length..], "yyyy-MM-dd");
            var offset = date.DayNumber - PlanToday.DayNumber;
            return HasPlan && offset is >= 0 and < 7
                ? Json(PlanDayJson(date, offset))
                : Json("""{"errorCode":"no_active_plan","error":"No active plan covers that date."}""", HttpStatusCode.NotFound);
        }
        if (url.Contains("api/MealPlans/") && url.EndsWith("/log"))
        {
            PlanLogsPosted.Add((url, JsonDocument.Parse(body).RootElement.Clone()));
            if (LogEntryStatus != HttpStatusCode.OK)
                return Json("""{"errorCode":"text_entry","error":"A text entry has nothing to log."}""", LogEntryStatus);
            var entryId = Guid.Parse(url.Split('/')[^2]);
            var index = PlanEntries.FindIndex(e => e.Id == entryId);
            if (index < 0) return Json("""{"errorCode":"not_found","error":"No such entry."}""", HttpStatusCode.NotFound);
            PlanEntries[index] = PlanEntries[index] with { Logged = true };
            return Json("""{"diaryEntryIds":[],"calories":0,"date":"2026-10-22","mealType":"LUNCH"}""");
        }

        if (url.Contains("api/ShoppingLists/items/") && url.EndsWith("/toggle"))
        {
            TogglesPatched.Add((url, JsonDocument.Parse(body).RootElement.Clone()));
            var itemId = Guid.Parse(url.Split('/')[^2]);
            var index = ShopItems.FindIndex(i => i.Id == itemId);
            if (index < 0) return new HttpResponseMessage(HttpStatusCode.NotFound);
            ShopItems[index] = ShopItems[index] with { Checked = JsonDocument.Parse(body).RootElement.GetProperty("isChecked").GetBoolean() };
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
        if (url.Contains("api/ShoppingLists?"))
        {
            return Json(HasShoppingList
                ? $$"""{"items":[{"id":"{{ShoppingListId}}","name":"Week 43","userId":"{{User}}","totalItems":{{ShopItems.Count}},"checkedItems":0,"needsReviewCount":1,"createdAt":"2026-10-20T00:00:00Z","updatedAt":"2026-10-21T00:00:00Z"}],"totalCount":1,"page":1,"pageSize":1}"""
                : """{"items":[],"totalCount":0,"page":1,"pageSize":1}""");
        }
        if (url.Contains($"api/ShoppingLists/{ShoppingListId}"))
        {
            return HasShoppingList ? Json(ShoppingListJson()) : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    // ---- the API's answers, built from the state above the way the read model would ----

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private string PlanDayJson(DateOnly date, int dayIndex)
    {
        var slots = PlanEntries.GroupBy(e => e.Slot).Select(g => new
        {
            slot = g.Key,
            entries = g.Select(e => new
            {
                id = e.Id,
                dayIndex,
                date,
                slot = e.Slot,
                kind = e.Kind,
                recipeTitle = e.Kind == "Recipe" ? e.Name : null,
                foodName = e.Kind == "Food" ? e.Name : null,
                text = e.Kind == "Text" ? e.Name : null,
                grams = e.Grams,
                servings = e.Servings,
                nutrition = e.Calories is { } kcal ? new { calories = kcal, proteinGrams = 10m, carbsGrams = 20m, fatGrams = 5m, fiberGrams = 1m } : null,
                logged = e.Logged,
            }),
        });

        return JsonSerializer.Serialize(new
        {
            planId = PlanId,
            planName = PlanName,
            date,
            day = new
            {
                dayIndex,
                date,
                slots,
                totals = new { calories = PlanEntries.Sum(e => e.Calories ?? 0), proteinGrams = 110m, carbsGrams = 150m, fatGrams = 40m, fiberGrams = 20m },
                unresolvedCount = 1,
                entryCount = PlanEntries.Count,
                loggedCount = PlanEntries.Count(e => e.Logged),
                violations = new[] { new { dayIndex, slot = (string?)null, rule = "MaxCarbs", message = "Day 1 has 150 g carbs, over the 120 g limit." } },
            },
            rules = new
            {
                macroMode = "Custom",
                targetCalories = 1800m,
                targetProtein = 140m,
                maxCarbs = 120m,
                eatingWindowStart = "08:00:00",
                eatingWindowEnd = "20:00:00",
                prepStyle = "Batch",
            },
        }, Web);
    }

    private string ShoppingListJson() => JsonSerializer.Serialize(new
    {
        id = ShoppingListId,
        name = "Week 43",
        userId = User,
        items = ShopItems.Select((i, n) => new
        {
            id = i.Id,
            itemName = i.Name,
            amount = i.Amount,
            unit = i.Unit,
            grams = i.Grams,
            needsReview = i.NeedsReview,
            prepSessionId = i.Session is null ? (Guid?)null : Guid.Parse("77777777-7777-4777-8777-777777777777"),
            prepSessionTitle = i.Session,
            sortOrder = n,
            isChecked = i.Checked,
        }),
        totalItems = ShopItems.Count,
        checkedItems = ShopItems.Count(i => i.Checked),
        needsReviewCount = ShopItems.Count(i => i.NeedsReview),
        canEdit = true,
    }, Web);
}

internal sealed record FakePlanEntry(Guid Id, string Slot, string Kind, string Name, decimal? Grams, decimal? Servings, decimal? Calories, bool Logged);

internal sealed record FakeShopItem(Guid Id, string Name, string? Session, decimal? Grams, decimal? Amount, string? Unit, bool NeedsReview, bool Checked);
