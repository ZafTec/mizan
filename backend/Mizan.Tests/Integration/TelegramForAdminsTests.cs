extern alias telegram;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Contracts.Planning;
using Mizan.Contracts.Telegram;
using Mizan.Contracts.Users;
using Mizan.Application.Telegram;
using telegram::Mizan.Telegram;
using telegram::Mizan.Telegram.Backend;
using telegram::Mizan.Telegram.Bot;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The bot holds the regular service key, which acts for an administrator as an ordinary user. So an admin's
/// chat works like anyone else's, and the API's notify call reaches the bot with the same key.
/// </summary>
[Collection("ApiIntegration")]
public class TelegramForAdminsTests
{
    private const string ServiceKey = "test-api-key";
    private readonly ApiTestFixture _fixture;

    public TelegramForAdminsTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnAdminAndAnOrdinaryUser_EachLinkTheBot_UsePlanAndNotifications_AndGetNotifiedInTheChat()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (coachId, coach) = await world.PersonAsync("coach", role: "trainer");
        var people = new List<(Guid Id, HttpClient Web, long Chat)>();
        foreach (var (role, chat) in new[] { ("admin", 2001L), ("user", 2002L) })
        {
            var (id, web) = await world.PersonAsync(role, role);
            await _fixture.GrantProAsync(id);
            await world.CoachingAsync(coachId, id, canPlanMeals: true);
            people.Add((id, web, chat));
        }

        var telegram = new FakeTelegram();
        var bot = Bot(telegram);

        foreach (var (_, web, chat) in people)
        {
            var issued = await web.PostAsync("/api/Telegram/link", null);
            issued.StatusCode.Should().Be(HttpStatusCode.OK);
            var code = (await issued.Content.ReadFromJsonAsync<TelegramLinkCodeDto>())!.Code;

            await bot.HandleAsync(Text(chat, $"/start {code}"), CancellationToken.None);
            telegram.LastText(chat).Should().StartWith("Connected.");

            await bot.HandleAsync(Text(chat, "/plan"), CancellationToken.None);
            telegram.LastText(chat).Should().StartWith("No active plan covers today", "the API answered for the account rather than refusing the bot");

            await bot.HandleAsync(Text(chat, "/notifications on"), CancellationToken.None);
            telegram.LastText(chat).Should().StartWith("Notifications are on.");
            (await web.GetFromJsonAsync<UserDto>("/api/Users/me"))!.NotifyByTelegram.Should().BeTrue();
        }

        foreach (var (id, _, _) in people)
        {
            var drafted = await coach.PostAsJsonAsync($"/api/Trainers/clients/{id}/meal-plans", new CreateMealPlanRequest
            {
                Name = "Week",
                StartDate = new DateOnly(2026, 10, 26),
                DurationDays = 7,
            });
            drafted.StatusCode.Should().Be(HttpStatusCode.Created, await drafted.Content.ReadAsStringAsync());
        }
        await _fixture.DrainOutboxAsync();

        // The API asks the bot with its own Mcp:ServiceApiKey, the key the bot itself holds.
        var asked = _fixture.TelegramBot.Sent;
        asked.Select(s => s.TelegramUserId).Should().BeEquivalentTo(people.Select(p => p.Chat));
        foreach (var request in asked)
        {
            (await NotifyAsync(telegram, request, ServiceKey)).Should().Be(StatusCodes.Status204NoContent);
            telegram.LastText(request.TelegramUserId).Should().StartWith("<b>Test User drafted a meal plan for you</b>");
        }
    }

    private IOptions<TelegramBotOptions> Options() => Microsoft.Extensions.Options.Options.Create(new TelegramBotOptions
    {
        BotToken = "123:test",
        ServiceApiKey = ServiceKey,
        ApiUrl = "http://localhost",
        PublicUrl = "https://mizan.test",
    });

    private UpdateHandler Bot(FakeTelegram telegram)
    {
        var options = Options();
        return new UpdateHandler(
            new TelegramClient(new HttpClient(telegram), options, NullLogger<TelegramClient>.Instance),
            new MizanApiClient(new HttpClient(_fixture.Server.CreateHandler()), options, NullLogger<MizanApiClient>.Instance),
            options,
            NullLogger<UpdateHandler>.Instance);
    }

    private async Task<int> NotifyAsync(FakeTelegram telegram, TelegramNotifyRequest request, string key)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        context.Request.Headers["X-Api-Key"] = key;
        context.Response.Body = new MemoryStream();

        var options = Options();
        await new NotifyEndpoint(new TelegramClient(new HttpClient(telegram), options, NullLogger<TelegramClient>.Instance), options, NullLogger<NotifyEndpoint>.Instance)
            .HandleAsync(context, CancellationToken.None);
        return context.Response.StatusCode;
    }

    private static Update Text(long chat, string text) => new()
    {
        UpdateId = 1,
        Message = new Message
        {
            MessageId = 10,
            From = new TelegramUser { Id = chat, FirstName = "T" },
            Chat = new Chat { Id = chat, Type = "private" },
            Text = text,
        },
    };

    /// <summary>Telegram's side of the wire: every call succeeds, and each sendMessage is kept per chat.</summary>
    private sealed class FakeTelegram : HttpMessageHandler
    {
        private readonly List<(long Chat, string Text)> _sent = [];

        public string LastText(long chat)
        {
            lock (_sent) return _sent.Last(s => s.Chat == chat).Text;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/sendMessage"))
            {
                var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement;
                lock (_sent) _sent.Add((body.GetProperty("chat_id").GetInt64(), body.GetProperty("text").GetString()!));
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"result":{}}""", Encoding.UTF8, "application/json"),
            };
        }
    }
}
