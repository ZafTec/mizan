using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Ai;
using Moq;
using Xunit;

namespace Mizan.Tests.Infrastructure;

public class OpenAiCompatibleProviderTests
{
    private const string TextResponse = """
        {
          "model": "gpt-5.6-luna",
          "choices": [{ "message": { "role": "assistant", "content": "A recorded meal." } }],
          "usage": {
            "prompt_tokens": 12,
            "completion_tokens": 34,
            "completion_tokens_details": { "reasoning_tokens": 20 }
          }
        }
        """;

    [Theory]
    [InlineData("https://mizan-test.openai.azure.com/openai/v1")]
    [InlineData("https://mizan-test.openai.azure.com/openai/v1/")]
    public async Task AzureV1UsesTheDeploymentAndKeyWithoutAnApiVersion(string baseUrl)
    {
        using var handler = new RecordingHandler(TextResponse);
        var provider = Provider(new AiOptions
        {
            BaseUrl = baseUrl,
            ApiKey = "test-azure-key",
            Model = "meal-assistant-deployment",
            MaxOutputTokens = 1024,
        }, handler);

        var response = await provider.CompleteAsync(new AiCompletionRequest
        {
            Messages = [new(AiRole.User, "Describe the meal.")],
            MaxOutputTokens = 2048,
        });

        handler.Method.Should().Be(HttpMethod.Post);
        handler.RequestUri.Should().Be(new Uri("https://mizan-test.openai.azure.com/openai/v1/chat/completions"));
        handler.Authorization.Should().Be("Bearer test-azure-key");
        handler.ApiKey.Should().Be("test-azure-key");
        handler.Body.GetProperty("model").GetString().Should().Be("meal-assistant-deployment");
        handler.Body.GetProperty("max_completion_tokens").GetInt32().Should().Be(2048);
        handler.Body.TryGetProperty("max_tokens", out _).Should().BeFalse();
        response.Content.Should().Be("A recorded meal.");
        response.Model.Should().Be("gpt-5.6-luna");
        response.Usage.Should().Be(new AiTokenUsage(12, 34));
    }

    [Fact]
    public async Task ToolRequestsDisableReasoningAndPreserveToolTurns()
    {
        const string parameters = """
            {"type":"object","properties":{"weightKg":{"type":"number"}},"required":["weightKg"],"additionalProperties":false}
            """;
        const string toolResponse = """
            {
              "choices": [{
                "message": {
                  "content": null,
                  "tool_calls": [{
                    "id": "call-next",
                    "type": "function",
                    "function": {"name":"record_weight","arguments":"{\"weightKg\":74.2}"}
                  }]
                }
              }],
              "usage": {"prompt_tokens": 50, "completion_tokens": 15}
            }
            """;
        using var handler = new RecordingHandler(toolResponse);
        var provider = Provider(AzureOptions(" none "), handler);

        var response = await provider.CompleteAsync(new AiCompletionRequest
        {
            Messages =
            [
                new(AiRole.System, "Only record the measurement the user provides."),
                new(AiRole.User, "Record 74.2 kg."),
                new(AiRole.Assistant, string.Empty)
                {
                    ToolCalls = [new("call-previous", "record_weight", "{\"weightKg\":74.1}")],
                },
                new(AiRole.Tool, "{\"saved\":true}") { ToolCallId = "call-previous" },
            ],
            Tools = [new("record_weight", "Record a bodyweight measurement.", parameters)],
            Temperature = 0.2,
        });

        handler.Body.GetProperty("reasoning_effort").GetString().Should().Be("none");
        handler.Body.TryGetProperty("temperature", out _).Should().BeFalse();
        var tool = handler.Body.GetProperty("tools")[0];
        tool.GetProperty("type").GetString().Should().Be("function");
        tool.GetProperty("function").GetProperty("name").GetString().Should().Be("record_weight");
        tool.GetProperty("function").GetProperty("parameters").GetProperty("required")[0]
            .GetString().Should().Be("weightKg");
        var messages = handler.Body.GetProperty("messages");
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[2].GetProperty("role").GetString().Should().Be("assistant");
        messages[2].TryGetProperty("content", out _).Should().BeFalse();
        messages[2].GetProperty("tool_calls")[0].GetProperty("id").GetString().Should().Be("call-previous");
        messages[3].GetProperty("role").GetString().Should().Be("tool");
        messages[3].GetProperty("tool_call_id").GetString().Should().Be("call-previous");
        messages[3].GetProperty("content").GetString().Should().Be("{\"saved\":true}");
        response.Content.Should().BeEmpty();
        response.ToolCalls.Should().ContainSingle().Which.Should()
            .Be(new AiToolCall("call-next", "record_weight", "{\"weightKg\":74.2}"));
        response.Usage.Should().Be(new AiTokenUsage(50, 15));
    }

    [Fact]
    public async Task StructuredImageRequestsKeepTheirSchemaAndImageContent()
    {
        const string schema = """
            {"type":"object","properties":{"foods":{"type":"array","items":{"type":"string"}}},"required":["foods"],"additionalProperties":false}
            """;
        byte[] image = [137, 80, 78, 71];
        using var handler = new RecordingHandler(TextResponse);
        var provider = Provider(AzureOptions("none"), handler);

        await provider.CompleteAsync(new AiCompletionRequest
        {
            Messages = [new(AiRole.User, "Identify these foods.", new AiImage(image, "image/png"))],
            ResponseSchema = new AiJsonSchema("food_photo", schema),
            Temperature = 0.4,
        });

        handler.Body.GetProperty("reasoning_effort").GetString().Should().Be("none");
        handler.Body.TryGetProperty("temperature", out _).Should().BeFalse();
        handler.Body.GetProperty("max_completion_tokens").GetInt32().Should().Be(1024);
        var parts = handler.Body.GetProperty("messages")[0].GetProperty("content");
        parts[0].GetProperty("type").GetString().Should().Be("text");
        parts[0].GetProperty("text").GetString().Should().Be("Identify these foods.");
        parts[1].GetProperty("type").GetString().Should().Be("image_url");
        parts[1].GetProperty("image_url").GetProperty("url").GetString()
            .Should().Be($"data:image/png;base64,{Convert.ToBase64String(image)}");
        var format = handler.Body.GetProperty("response_format");
        format.GetProperty("type").GetString().Should().Be("json_schema");
        var jsonSchema = format.GetProperty("json_schema");
        jsonSchema.GetProperty("name").GetString().Should().Be("food_photo");
        jsonSchema.GetProperty("strict").GetBoolean().Should().BeTrue();
        jsonSchema.GetProperty("schema").GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        jsonSchema.GetProperty("schema").GetProperty("properties").GetProperty("foods")
            .GetProperty("items").GetProperty("type").GetString().Should().Be("string");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task UnsetReasoningEffortIsOmittedForOtherCompatibleProviders(string? reasoningEffort)
    {
        using var handler = new RecordingHandler(TextResponse);
        var provider = Provider(new AiOptions
        {
            BaseUrl = "https://provider.example/v1",
            ApiKey = "test-provider-key",
            Model = "configured-model",
            ReasoningEffort = reasoningEffort,
            SupportsTemperature = true,
        }, handler);

        await provider.CompleteAsync(new AiCompletionRequest
        {
            Messages = [new(AiRole.User, "Hello.")],
            Temperature = 0.3,
        });

        handler.Body.TryGetProperty("reasoning_effort", out _).Should().BeFalse();
        handler.Body.GetProperty("temperature").GetDouble().Should().Be(0.3);
        handler.Body.TryGetProperty("tools", out _).Should().BeFalse();
        handler.Body.TryGetProperty("response_format", out _).Should().BeFalse();
    }

    [Fact]
    public async Task AProviderRefusalIsLoggedWithItsMessage_AndNotReturnedToTheCaller()
    {
        const string refusal = """
            {"error":{"type":"invalid_request_error","code":"unsupported_value","param":"temperature",
              "message":"Unsupported value for temperature with key test-azure-key. Echo: Describe the meal."}}
            """;
        using var handler = new RecordingHandler((HttpStatusCode.BadRequest, refusal));
        var logger = new RecordingLogger();
        var provider = Provider(AzureOptions("none"), handler, logger);

        var call = () => provider.CompleteAsync(new AiCompletionRequest { Messages = [new(AiRole.User, "Describe the meal.")] });

        var thrown = (await call.Should().ThrowAsync<Mizan.Application.Exceptions.AiProviderRejectedException>()).Which;
        thrown.Message.Should().Be(Mizan.Application.Exceptions.AiProviderRejectedException.MisconfiguredMessage);
        thrown.Reason.Should().Be("http_400");
        thrown.IsConfigurationError.Should().BeTrue();
        thrown.ProviderParam.Should().Be("temperature");
        thrown.ProviderMessage.Should().Contain("Unsupported value for temperature").And.NotContain("test-azure-key");
        var line = logger.Lines.Should().ContainSingle().Which;
        line.Level.Should().Be(LogLevel.Warning);
        line.Text.Should().Contain("400").And.Contain("invalid_request_error").And.Contain("Unsupported value for temperature")
            .And.NotContain("test-azure-key");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "The assistant returned an error (429). Try again shortly.", false)]
    [InlineData(HttpStatusCode.BadGateway, "The assistant returned an error (502). Try again shortly.", false)]
    [InlineData(HttpStatusCode.Unauthorized, "The assistant is misconfigured. An administrator has been told.", true)]
    public async Task OnlyARefusalThatRetryingCannotFix_SaysTheAssistantIsMisconfigured(HttpStatusCode status, string message, bool configuration)
    {
        using var handler = new RecordingHandler((status, "not json"));
        var provider = Provider(AzureOptions("none"), handler);

        var call = () => provider.CompleteAsync(new AiCompletionRequest { Messages = [new(AiRole.User, "Hello.")] });

        var thrown = (await call.Should().ThrowAsync<Mizan.Application.Exceptions.AiProviderRejectedException>()).Which;
        thrown.Message.Should().Be(message);
        thrown.IsConfigurationError.Should().Be(configuration);
        thrown.ProviderMessage.Should().BeNull("a body that is not an error object may quote the request");
    }

    [Fact]
    public async Task AToolRequestRefusedForItsReasoning_IsSentAgainWithReasoningOff_AndLaterOnesStartThere()
    {
        const string refusal = """
            {"error":{"code":"BadRequest","message":"Function tools with reasoning_effort are not supported for gpt-6-luna in /v1/chat/completions. To use function tools, use /v1/responses or set reasoning_effort to 'none'."}}
            """;
        using var handler = new RecordingHandler((HttpStatusCode.BadRequest, refusal), (HttpStatusCode.OK, TextResponse), (HttpStatusCode.OK, TextResponse));
        var provider = Provider(AzureOptions(""), handler);
        var request = new AiCompletionRequest
        {
            Messages = [new(AiRole.User, "Plan my week.")],
            Tools = [new("ping", "Answers pong.", """{"type":"object","properties":{},"additionalProperties":false}""")],
        };

        var response = await provider.CompleteAsync(request);
        await provider.CompleteAsync(request);

        response.Content.Should().Be("A recorded meal.");
        handler.Bodies.Should().HaveCount(3);
        handler.Bodies[0].TryGetProperty("reasoning_effort", out _).Should().BeFalse();
        handler.Bodies[1].GetProperty("reasoning_effort").GetString().Should().Be("none");
        handler.Bodies[2].GetProperty("reasoning_effort").GetString().Should().Be("none", "the endpoint is remembered");
    }

    private static AiOptions AzureOptions(string reasoningEffort) => new()
    {
        BaseUrl = "https://mizan-test.openai.azure.com/openai/v1",
        ApiKey = "test-azure-key",
        Model = "gpt-5.6-luna",
        ReasoningEffort = reasoningEffort,
        SupportsTemperature = false,
    };

    private static OpenAiCompatibleProvider Provider(AiOptions options, RecordingHandler handler, RecordingLogger? logger = null)
    {
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(factory => factory.CreateClient(OpenAiCompatibleProvider.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return new OpenAiCompatibleProvider(new FixedRuntimeOptions<AiOptions>(options), clients.Object, logger ?? new RecordingLogger());
    }

    /// <summary>Answers each request with the next scripted status and body; the last one repeats.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;
        private (HttpStatusCode Status, string Body) _last;

        public RecordingHandler(string response) : this((HttpStatusCode.OK, response)) { }

        public RecordingHandler(params (HttpStatusCode Status, string Body)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
            _last = responses[^1];
        }

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public string? ApiKey { get; private set; }
        public JsonElement Body { get; private set; }
        public List<JsonElement> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            ApiKey = request.Headers.GetValues("api-key").Single();
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Body = document.RootElement.Clone();
            Bodies.Add(Body);
            var (status, body) = _responses.Count > 0 ? _responses.Dequeue() : _last;
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class RecordingLogger : ILogger<OpenAiCompatibleProvider>
    {
        public List<(LogLevel Level, string Text)> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add((logLevel, formatter(state, exception)));
    }
}
