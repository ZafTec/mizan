extern alias telegram;
using System.Net;
using System.Text;
using System.Text.Json;
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

    // ---- what the bot asked for ----

    public List<JsonElement> MealsPosted { get; } = [];
    public List<JsonElement> ChatPosted { get; } = [];
    public List<JsonElement> LinkPosted { get; } = [];

    public JsonElement LastSend => JsonDocument.Parse(_telegramSends.Last(s => s.Url.EndsWith("/sendMessage")).Body).RootElement.Clone();

    public string LastText => LastSend.GetProperty("text").GetString()!;

    public List<(string Text, string Data)> LastKeyboard() =>
        LastSend.GetProperty("reply_markup").GetProperty("inline_keyboard").EnumerateArray().SelectMany(row => row.EnumerateArray())
            .Select(b => (b.GetProperty("text").GetString()!, b.GetProperty("callback_data").GetString()!)).ToList();

    public UpdateHandler Handler()
    {
        var options = Options.Create(new TelegramBotOptions
        {
            BotToken = "token",
            ServiceApiKey = "key",
            ApiUrl = "http://api.test",
            PublicUrl = "https://mizan.test",
        });
        return new UpdateHandler(
            new TelegramClient(new HttpClient(this), options, NullLogger<TelegramClient>.Instance),
            new MizanApiClient(new HttpClient(this), options, NullLogger<MizanApiClient>.Instance),
            options,
            NullLogger<UpdateHandler>.Instance);
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
        if (url.Contains("api/Meals"))
        {
            MealsPosted.Add(JsonDocument.Parse(body).RootElement.Clone());
            return MealsStatus == HttpStatusCode.OK ? Json("{}") : Json("""{"errorCode":"bad_request","error":"No."}""", MealsStatus);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
