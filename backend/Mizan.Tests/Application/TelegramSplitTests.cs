extern alias telegram;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using telegram::Mizan.Telegram;
using telegram::Mizan.Telegram.Backend;
using telegram::Mizan.Telegram.Bot;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// A meal card offers three things: log it, split it with the people who ate it, or discard it. The bot holds
/// no state, so each button carries what it needs, and none may go over Telegram's 64-byte limit.
/// </summary>
public class TelegramSplitTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private readonly FakeWorld _world = new();

    [Fact]
    public async Task APhoto_GetsLogSplitAndDiscard()
    {
        var handler = _world.Handler();

        await handler.HandleAsync(Photo(), CancellationToken.None);

        var buttons = _world.LastKeyboard();
        buttons.Select(b => b.Text).Should().Equal("Log it", "Split", "Discard");
        buttons[0].Data.Should().StartWith("log:");
        buttons[1].Data.Should().StartWith("split:");
        buttons[2].Data.Should().Be("discard");
        _world.MealsPosted.Should().BeEmpty("a proposal is never a silent write");
    }

    [Fact]
    public async Task EveryButton_StaysUnderTelegramsByteLimit_EvenForALongNameInAnotherScript()
    {
        _world.FoodName = string.Concat(Enumerable.Repeat("የኢትዮጵያ ምግብ ", 12));
        var handler = _world.Handler();

        await handler.HandleAsync(Photo(), CancellationToken.None);

        var card = _world.LastKeyboard();
        card.Should().OnlyContain(b => Encoding.UTF8.GetByteCount(b.Data) <= 64);

        await handler.HandleAsync(Press(card[1].Data), CancellationToken.None);
        var picker = _world.LastKeyboard();
        picker.Should().OnlyContain(b => Encoding.UTF8.GetByteCount(b.Data) <= 64);
        picker.Should().Contain(b => b.Data.StartsWith("share:5|"));
    }

    [Fact]
    public async Task Split_AsksForThePeople_AndLogsNothingYet()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);

        await handler.HandleAsync(Press(_world.LastKeyboard()[1].Data), CancellationToken.None);

        _world.LastKeyboard().Select(b => b.Text).Should().Equal("2", "3", "4", "5", "Log it all", "Discard");
        _world.LastText.Should().Contain("How many people");
        _world.MealsPosted.Should().BeEmpty();
    }

    [Fact]
    public async Task ChoosingThree_LogsOneThirdOfTheMeal()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);
        await handler.HandleAsync(Press(_world.LastKeyboard()[1].Data), CancellationToken.None);

        await handler.HandleAsync(Press(_world.LastKeyboard().Single(b => b.Text == "3").Data), CancellationToken.None);

        var meal = _world.MealsPosted.Should().ContainSingle().Subject;
        meal.GetProperty("calories").GetDecimal().Should().Be(200m, "600 kcal shared by three");
        meal.GetProperty("proteinGrams").GetDecimal().Should().Be(10m);
        meal.GetProperty("carbsGrams").GetDecimal().Should().Be(20m);
        meal.GetProperty("fatGrams").GetDecimal().Should().Be(6.7m);
        meal.GetProperty("name").GetString().Should().EndWith("(1/3 share)");
        _world.LastText.Should().Contain("1/3 share").And.Contain("200 kcal");
    }

    [Fact]
    public async Task LogItAll_FromThePicker_LogsTheWholeMeal()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);
        await handler.HandleAsync(Press(_world.LastKeyboard()[1].Data), CancellationToken.None);

        await handler.HandleAsync(Press(_world.LastKeyboard().Single(b => b.Text == "Log it all").Data), CancellationToken.None);

        _world.MealsPosted.Should().ContainSingle().Which.GetProperty("calories").GetDecimal().Should().Be(600m);
    }

    [Fact]
    public async Task LogIt_StillLogsTheWholeMeal_AsBefore()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);

        await handler.HandleAsync(Press(_world.LastKeyboard()[0].Data), CancellationToken.None);

        var meal = _world.MealsPosted.Should().ContainSingle().Subject;
        meal.GetProperty("calories").GetDecimal().Should().Be(600m);
        meal.GetProperty("name").GetString().Should().NotContain("share");
    }

    [Fact]
    public async Task Discard_LogsNothing()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);

        await handler.HandleAsync(Press("discard"), CancellationToken.None);

        _world.MealsPosted.Should().BeEmpty();
        _world.LastText.Should().Contain("Discarded");
    }

    [Theory]
    [InlineData("share:1|600|30|60|20|Pasta")]
    [InlineData("share:9|600|30|60|20|Pasta")]
    [InlineData("share:0|600|30|60|20|Pasta")]
    [InlineData("share:x|600|30|60|20|Pasta")]
    [InlineData("share:3|nonsense")]
    [InlineData("split:nonsense")]
    public async Task AButtonThatMakesNoSense_LogsNothing(string data)
    {
        var handler = _world.Handler();

        await handler.HandleAsync(Press(data), CancellationToken.None);

        _world.MealsPosted.Should().BeEmpty();
        _world.LastText.Should().Contain("too old");
    }

    [Fact]
    public async Task ALapsedAccount_CannotSplit()
    {
        _world.IsPro = false;
        var handler = _world.Handler();

        await handler.HandleAsync(Press("share:3|600|30|60|20|Pasta"), CancellationToken.None);

        _world.MealsPosted.Should().BeEmpty();
        _world.LastText.Should().Contain("Mizan Pro");
    }

    [Fact]
    public async Task WhenTheApiRefusesTheShare_TheUserIsToldAndNothingIsClaimed()
    {
        _world.MealsStatus = HttpStatusCode.BadRequest;
        var handler = _world.Handler();

        await handler.HandleAsync(Press("share:2|600|30|60|20|Pasta"), CancellationToken.None);

        _world.LastText.Should().NotContain("Logged your");
    }

    [Theory]
    [InlineData("Pasta", 100, "Pasta")]
    [InlineData("Pasta", 3, "Pas")]
    [InlineData("ምግብ", 6, "ምግ")]
    [InlineData("ምግብ", 5, "ም")]
    [InlineData("anything", 0, "")]
    public void TruncatingByBytes_NeverCutsACharacterInHalf(string value, int bytes, string expected)
    {
        UpdateHandler.TruncateUtf8(value, bytes).Should().Be(expected);
    }

    [Fact]
    public void AShare_IsRoundedToWholeCalories_AndTenthsOfAGram()
    {
        var share = UpdateHandler.ShareOf(new UpdateHandler.LoggedMeal("Stew", 1000m, 31m, 61m, 11m), 3);

        share.Calories.Should().Be(333m);
        share.ProteinGrams.Should().Be(10.3m);
        share.CarbsGrams.Should().Be(20.3m);
        share.FatGrams.Should().Be(3.7m);
    }

    // ---- the world around the handler ----

    private static Update Photo() => new()
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

    private static Update Press(string data) => new()
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

    private sealed record Button(string Text, string Data);

    private sealed class FakeWorld : HttpMessageHandler
    {
        private readonly List<(string Url, string Body)> _telegramSends = [];

        public bool IsPro { get; set; } = true;
        public string FoodName { get; set; } = "Pasta";
        public HttpStatusCode MealsStatus { get; set; } = HttpStatusCode.OK;
        public List<JsonElement> MealsPosted { get; } = [];

        public string LastText => JsonDocument.Parse(_telegramSends.Last(s => s.Url.EndsWith("/sendMessage")).Body).RootElement.GetProperty("text").GetString()!;

        public List<Button> LastKeyboard()
        {
            var send = _telegramSends.Last(s => s.Url.EndsWith("/sendMessage"));
            var markup = JsonDocument.Parse(send.Body).RootElement.GetProperty("reply_markup");
            return markup.GetProperty("inline_keyboard").EnumerateArray().SelectMany(row => row.EnumerateArray())
                .Select(b => new Button(b.GetProperty("text").GetString()!, b.GetProperty("callback_data").GetString()!)).ToList();
        }

        public UpdateHandler Handler()
        {
            var options = Options.Create(new TelegramBotOptions
            {
                BotToken = "token", ServiceApiKey = "key", ApiUrl = "http://api.test", PublicUrl = "https://mizan.test",
            });
            return new UpdateHandler(
                new TelegramClient(new HttpClient(this), options, NullLogger<TelegramClient>.Instance),
                new MizanApiClient(new HttpClient(this), options, NullLogger<MizanApiClient>.Instance),
                options,
                NullLogger<UpdateHandler>.Instance);
        }

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
                return Json($$"""{"userId":"{{User}}","name":"Tester","linkedAt":"2026-10-01T00:00:00Z","isPro":{{IsPro.ToString().ToLowerInvariant()}}}""");
            if (url.Contains("api/Nutrition/ai/analyze-image"))
                return Json($$"""
                    {"foods":[{"name":"{{FoodName}}","portionGrams":300,"calories":600,"protein":30,"carbs":60,"fat":20}],
                     "totalCalories":600,"confidence":0.8,"note":null}
                    """);
            if (url.Contains("api/Meals"))
            {
                MealsPosted.Add(JsonDocument.Parse(body).RootElement.Clone());
                return MealsStatus == HttpStatusCode.OK ? Json("{}") : Json("""{"errorCode":"bad_request","error":"No."}""", MealsStatus);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
