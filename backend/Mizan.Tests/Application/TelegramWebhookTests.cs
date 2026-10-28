extern alias telegram;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using telegram::Mizan.Telegram;
using telegram::Mizan.Telegram.Bot;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// Webhook registration in webhook mode. Telegram's refusal must be seen (its reason logged and on /health) and
/// retried, and success is claimed only once Telegram has answered <c>ok: true</c>.
/// </summary>
public class TelegramWebhookTests
{
    private const string Refusal = """{"ok":false,"error_code":400,"description":"Bad Request: bad webhook: Failed to resolve host: Name or service not known"}""";

    [Fact]
    public async Task ARefusedRegistration_IsReportedWithTelegramsReason_RetriedWithBackoff_AndRegisteredOnceAccepted()
    {
        var telegram = new ScriptedTelegram((HttpStatusCode.BadRequest, Refusal), (HttpStatusCode.BadRequest, Refusal), (HttpStatusCode.OK, """{"ok":true,"result":true}"""));
        var state = new WebhookState();
        var logs = new ListLogger();
        var waits = new List<TimeSpan>();
        var states = new List<WebhookStatus>();
        var registrar = new WebhookRegistrar(state, logs, (wait, _) =>
        {
            waits.Add(wait);
            states.Add(state.Current);
            return Task.CompletedTask;
        });

        await registrar.RegisterAsync(Client(telegram), Options, CancellationToken.None);

        states.Should().AllSatisfy(s =>
        {
            s.State.Should().Be("failed");
            s.LastError.Should().Be("Bad Request: bad webhook: Failed to resolve host: Name or service not known");
        });
        waits.Should().Equal(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        state.Current.Should().Be(new WebhookStatus("registered", null, 3));
        telegram.Calls("setWebhook").Should().Be(3);
        logs.Lines.Where(l => l.Contains("webhook registered")).Should().ContainSingle();
        logs.Lines.Should().Contain(l => l.Contains("Failed to resolve host"));
    }

    [Fact]
    public async Task OkFalse_WithHttp200_IsAFailureToo()
    {
        var telegram = new ScriptedTelegram((HttpStatusCode.OK, """{"ok":false,"description":"Bad Request: secret token contains unallowed characters"}"""));
        var state = new WebhookState();
        var logs = new ListLogger();

        var registered = await new WebhookRegistrar(state, logs, (_, _) => Task.CompletedTask)
            .TryRegisterAsync(Client(telegram), Options, 1, CancellationToken.None);

        registered.Should().BeFalse();
        state.Current.State.Should().Be("failed");
        state.Current.LastError.Should().Be("Bad Request: secret token contains unallowed characters");
        logs.Lines.Should().NotContain(l => l.Contains("webhook registered"));
    }

    private static readonly TelegramBotOptions Options = new()
    {
        BotToken = "123:test",
        ServiceApiKey = "key",
        UseWebhook = true,
        WebhookSecret = "secret",
        PublicUrl = "https://mizan.test",
    };

    private static TelegramClient Client(ScriptedTelegram telegram) =>
        new(new HttpClient(telegram), Microsoft.Extensions.Options.Options.Create(Options), NullLogger<TelegramClient>.Instance);

    /// <summary>Answers setWebhook from a script, one answer per call, the last repeating; everything else is ok.</summary>
    private sealed class ScriptedTelegram(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
    {
        private readonly List<string> _methods = [];

        public int Calls(string method) => _methods.Count(m => m == method);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var method = request.RequestUri!.AbsolutePath.Split('/').Last();
            _methods.Add(method);
            var (status, body) = method == "setWebhook"
                ? answers[Math.Min(Calls("setWebhook") - 1, answers.Length - 1)]
                : (HttpStatusCode.OK, """{"ok":true,"result":{"url":"https://mizan.test/telegram/webhook","pending_update_count":0}}""");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class ListLogger : ILogger<WebhookRegistrar>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }
}
