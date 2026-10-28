extern alias telegram;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Ai;
using Mizan.Tests.Configuration;
using Xunit;
using TelegramBotProgram = telegram::Program;
using TelegramClient = telegram::Mizan.Telegram.Bot.TelegramClient;
using MizanApiClient = telegram::Mizan.Telegram.Backend.MizanApiClient;

namespace Mizan.Tests.Integration;

/// <summary>
/// Settings that used to need a redeploy, changed in the admin area and taking effect at once, through the API
/// (docs/ADMIN.md#runtime-settings).
/// </summary>
[Collection("ApiIntegration")]
public class RuntimeSettingsStoryTests
{
    private readonly ApiTestFixture _fixture;

    public RuntimeSettingsStoryTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnAdministratorConfiguresStorage_AndAnImageUploads()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await AdminAsync();
        await using var s3 = await FakeS3.StartAsync();

        // Nothing configured: the upload is refused, and the settings page says why.
        (await admin.PostAsync("/api/Uploads/image", Png())).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        Group(await admin.GetFromJsonAsync<JsonElement>("/api/admin/settings"), "storage")
            .GetProperty("notice").GetString().Should().Be("Storage is not configured. Image uploads are refused.");

        await PutAsync(admin, "Storage.ServiceUrl", s3.Url);
        await PutAsync(admin, "Storage.AccessKeyId", "story-access-key");
        await PutAsync(admin, "Storage.SecretAccessKey", "story-secret-key");
        await PutAsync(admin, "Storage.Bucket", "story-bucket");
        await PutAsync(admin, "Storage.PublicBaseUrl", "https://media.example.test");

        var uploaded = await admin.PostAsync("/api/Uploads/image", Png());
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var body = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        var key = body.GetProperty("key").GetString()!;
        body.GetProperty("url").GetString().Should().Be($"https://media.example.test/{key}");
        s3.Puts.Should().ContainSingle().Which.Should().Be($"/story-bucket/{key}");
        Group(await admin.GetFromJsonAsync<JsonElement>("/api/admin/settings"), "storage")
            .TryGetProperty("notice", out var notice).Should().BeTrue();
        notice.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AnAdministratorChangesTheAiProvider_AndTheNextReplyComesFromTheNewOne()
    {
        await _fixture.ResetDatabaseAsync();
        var provider = new FakeChatCompletions();
        await using var withProvider = _fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            // The real provider, reading its settings per call, against a scripted endpoint.
            services.RemoveAll<IAiProvider>();
            services.AddSingleton<IAiProvider, OpenAiCompatibleProvider>();
            services.AddHttpClient(OpenAiCompatibleProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => provider);
        }));
        var admin = await AdminAsync(withProvider);

        await PutAsync(admin, "Ai.BaseUrl", "https://first.example/v1");
        await PutAsync(admin, "Ai.ApiKey", "first-key");
        await PutAsync(admin, "Ai.Model", "model-a");
        var first = await (await admin.PostAsync("/api/admin/settings/ai/test", null)).Content.ReadFromJsonAsync<JsonElement>();
        first.GetProperty("reachable").GetBoolean().Should().BeTrue(first.ToString());
        first.GetProperty("model").GetString().Should().Be("model-a");

        await PutAsync(admin, "Ai.BaseUrl", "https://second.example/v1");
        await PutAsync(admin, "Ai.Model", "model-b");
        var second = await (await admin.PostAsync("/api/admin/settings/ai/test", null)).Content.ReadFromJsonAsync<JsonElement>();
        second.GetProperty("reachable").GetBoolean().Should().BeTrue(second.ToString());
        second.GetProperty("model").GetString().Should().Be("model-b");

        provider.Hosts.Should().StartWith("first.example").And.EndWith("second.example");
        provider.Hosts.Distinct().Should().Equal("first.example", "second.example");
    }

    [Fact]
    public async Task AnAdministratorRotatesTheWebhookSecret_AndTheBotReceivesTheNextUpdate()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await AdminAsync();
        await PutAsync(admin, "Telegram.BotToken", "123456:story-token");
        await PutAsync(admin, "Telegram.UseWebhook", "true");
        await PutAsync(admin, "Telegram.WebhookSecret", "first-secret");

        var telegramApi = new FakeTelegram();
        await using var bot = BotFactory(telegramApi);
        using var botClient = bot.CreateClient();
        _fixture.TelegramBot.OnSettingsChanged = async ct =>
        {
            using var signal = new HttpRequestMessage(HttpMethod.Post, "/internal/settings-changed");
            signal.Headers.Add("X-Api-Key", "test-api-key");
            (await botClient.SendAsync(signal, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        };

        try
        {
            // At startup the bot reads its settings from the API and registers the webhook with them.
            await telegramApi.WaitForWebhookAsync("first-secret");

            await PutAsync(admin, "Telegram.WebhookSecret", "second-secret");
            await telegramApi.WaitForWebhookAsync("second-secret");
            telegramApi.WebhookUrls.Should().AllBe("https://mizan.test/telegram/webhook");

            (await PostUpdateAsync(botClient, "first-secret")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await PostUpdateAsync(botClient, "second-secret")).StatusCode.Should().Be(HttpStatusCode.OK);
            await telegramApi.WaitForMessageAsync(ChatId);
            telegramApi.Token.Should().Be("123456:story-token");
        }
        finally
        {
            _fixture.TelegramBot.OnSettingsChanged = null;
        }
    }

    [Fact]
    public void TheBotService_BindsOnlyItsOwnSectionFromConfiguration()
    {
        IReadOnlyList<ServiceDescriptor> registrations = [];
        using var bot = BotFactory(new FakeTelegram(), services => registrations = services.ToList());
        _ = bot.Services;

        ConfigurationBindings.Sections(registrations)
            .Where(b => b.Options.Assembly.GetName().Name!.StartsWith("Mizan", StringComparison.Ordinal))
            .Should().ContainSingle().Which.Path.Should().Be("TelegramBot",
                "the bot's token, webhook and Mini App are runtime settings it reads from the API");
    }

    private const long ChatId = 424242;

    private WebApplicationFactory<TelegramBotProgram> BotFactory(FakeTelegram telegramApi, Action<IServiceCollection>? capture = null) =>
        new WebApplicationFactory<TelegramBotProgram>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("TelegramBot:ApiUrl", "http://localhost");
            builder.UseSetting("TelegramBot:ServiceApiKey", "test-api-key");
            builder.UseSetting("TelegramBot:PublicUrl", "https://mizan.test");
            builder.ConfigureTestServices(services =>
            {
                capture?.Invoke(services);
                services.AddHttpClient<MizanApiClient>().ConfigurePrimaryHttpMessageHandler(() => _fixture.Server.CreateHandler());
                services.AddHttpClient<TelegramClient>().ConfigurePrimaryHttpMessageHandler(() => telegramApi);
            });
        });

    private static async Task<HttpResponseMessage> PostUpdateAsync(HttpClient bot, string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/telegram/webhook")
        {
            Content = new StringContent(
                $$$"""{"update_id":7,"message":{"message_id":1,"from":{"id":{{{ChatId}}},"is_bot":false,"first_name":"Story"},"chat":{"id":{{{ChatId}}},"type":"private"},"text":"/help"}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        return await bot.SendAsync(request);
    }

    private async Task<HttpClient> AdminAsync(WebApplicationFactory<Program>? factory = null)
    {
        var id = Guid.NewGuid();
        var email = $"story-admin-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email, role: "admin");
        if (factory is null) return _fixture.CreateAuthenticatedClient(id, email, "admin");

        var token = await _fixture.CreateSessionAsync(id);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{ApiTestFixture.SessionCookieName}={token}");
        return client;
    }

    private static async Task PutAsync(HttpClient client, string key, string value)
    {
        var response = await client.PutAsJsonAsync($"/api/admin/settings/{key}", new { value });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static JsonElement Group(JsonElement listing, string id) =>
        listing.GetProperty("groups").EnumerateArray().Single(g => g.GetProperty("id").GetString() == id);

    private static MultipartFormDataContent Png()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 };
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { part, "file", "story.png" } };
    }

    /// <summary>An S3 endpoint on a real port that takes every PUT, since the SDK speaks HTTP to it.</summary>
    private sealed class FakeS3 : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private FakeS3(WebApplication app) => _app = app;

        public ConcurrentQueue<string> Puts { get; } = new();

        public string Url => _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        public static async Task<FakeS3> StartAsync()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            var app = builder.Build();
            var fake = new FakeS3(app);
            app.Run(async context =>
            {
                await context.Request.Body.CopyToAsync(Stream.Null);
                if (HttpMethods.IsPut(context.Request.Method)) fake.Puts.Enqueue(context.Request.Path.Value!);
                context.Response.Headers.ETag = "\"story\"";
                context.Response.StatusCode = StatusCodes.Status200OK;
            });
            await app.StartAsync();
            return fake;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    /// <summary>An OpenAI-compatible endpoint that answers "ok" with the model it was asked for.</summary>
    private sealed class FakeChatCompletions : HttpMessageHandler
    {
        private readonly ConcurrentQueue<string> _hosts = new();

        public IReadOnlyList<string> Hosts => _hosts.ToList();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _hosts.Enqueue(request.RequestUri!.Host);
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement;
            var model = body.GetProperty("model").GetString();
            var asksForTool = body.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array;
            var message = asksForTool
                ? """{"role":"assistant","content":null,"tool_calls":[{"id":"call_1","type":"function","function":{"name":"ping","arguments":"{}"}}]}"""
                : """{"role":"assistant","content":"ok"}""";
            var answer = $$$"""{"model":"{{{model}}}","choices":[{"message":{{{message}}}}],"usage":{"prompt_tokens":5,"completion_tokens":1}}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    /// <summary>The Bot API: records the webhooks registered and the messages sent, and accepts everything.</summary>
    private sealed class FakeTelegram : HttpMessageHandler
    {
        private readonly ConcurrentQueue<(string Url, string Secret)> _webhooks = new();
        private readonly ConcurrentQueue<long> _messages = new();

        public string? Token { get; private set; }

        public IReadOnlyList<string> WebhookUrls => _webhooks.Select(w => w.Url).ToList();

        public Task WaitForWebhookAsync(string secret) =>
            WaitAsync(() => _webhooks.Any(w => w.Secret == secret), $"a webhook registered with the secret {secret}");

        public Task WaitForMessageAsync(long chatId) =>
            WaitAsync(() => _messages.Contains(chatId), $"a message to chat {chatId}");

        private static async Task WaitAsync(Func<bool> condition, string what)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException($"Telegram never saw {what}.");
                await Task.Delay(50);
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // https://api.telegram.org/bot{token}/{method}
            var segments = request.RequestUri!.AbsolutePath.Trim('/').Split('/');
            Token = segments[0]["bot".Length..];
            var method = segments[^1];
            var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body.Length == 0 ? "{}" : body);

            switch (method)
            {
                case "setWebhook":
                    _webhooks.Enqueue((json.RootElement.GetProperty("url").GetString()!, json.RootElement.GetProperty("secret_token").GetString()!));
                    break;
                case "sendMessage":
                    _messages.Enqueue(json.RootElement.GetProperty("chat_id").GetInt64());
                    break;
            }

            var result = method == "getWebhookInfo"
                ? """{"ok":true,"result":{"url":"https://mizan.test/telegram/webhook","pending_update_count":0}}"""
                : method == "sendMessage"
                    ? """{"ok":true,"result":{"message_id":1,"chat":{"id":1,"type":"private"}}}"""
                    : """{"ok":true,"result":true}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(result, Encoding.UTF8, "application/json") };
        }
    }
}
