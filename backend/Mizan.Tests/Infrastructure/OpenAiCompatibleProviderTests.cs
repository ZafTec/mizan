using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Exceptions;
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
    [InlineData(HttpStatusCode.InternalServerError, "The assistant returned an error (500). Try again shortly.", false)]
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

    private const string GatewayUrl = "https://gateway.test/v1";

    private static AiOptions GatewayOptions() => new()
    {
        BaseUrl = GatewayUrl,
        ApiKey = "test-gateway-key",
        Model = "route-alias",
    };

    /// <summary>One gateway refusal each: what it answers, and what the person and every surface get.</summary>
    public static TheoryData<string, int, string, string?, string?, Type, string, string> Refusals => new()
    {
        { "a budget stop with its reset", 402, """{"error":{"code":"budget_exhausted","message":"Budget spent.","request_id":"req-1","resets_at":"2031-01-02T03:04:05Z"}}""", null, null,
            typeof(AiLimitReachedException), "ai_limit_reached", "The assistant has reached its limit for now. It should be back on 2031-01-02 at 03:04 UTC." },
        { "a usage stop with no reset", 402, """{"error":{"code":"usage_stop_reached","message":"Stopped."}}""", null, null,
            typeof(AiLimitReachedException), "ai_limit_reached", "The assistant has reached its limit for now." },
        { "a rate limit in seconds", 429, """{"error":{"code":"rate_limited","message":"Slow down."}}""", "Retry-After", "120",
            typeof(AiRateLimitedException), "ai_rate_limited", "The assistant is busy right now. Try again in 2 minutes." },
        { "a long window from RateLimit alone", 429, """{"error":{"code":"end_user_rate_limited","message":"Slow down."}}""", "RateLimit", "\"burst\";r=5;t=10, \"week\";r=0;t=7200",
            typeof(AiRateLimitedException), "ai_rate_limited", "The assistant is busy right now. Try again in 2 hours." },
        { "a guardrail", 403, """{"error":{"code":"guardrail_blocked","message":"Refused."}}""", null, null,
            typeof(AiBlockedException), "ai_blocked", "I can't help with that." },
        { "a blocked person", 403, """{"error":{"code":"end_user_blocked","message":"Blocked."}}""", null, null,
            typeof(AiAccountBlockedException), "ai_account_blocked", "The assistant isn't available for your account." },
        { "a wrong end-user token", 401, """{"error":{"code":"end_user_token_invalid","message":"Bad token."}}""", null, null,
            typeof(AiProviderRejectedException), "ai_unavailable", AiProviderRejectedException.MisconfiguredMessage },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task AGatewayRefusal_BecomesItsOwnErrorForEverySurface_WithoutARetry(
        string story, int status, string body, string? header, string? headerValue, Type expected, string code, string message)
    {
        using var handler = new RecordingHandler(((HttpStatusCode)status, body));
        if (header is not null) handler.ResponseHeaders = response => response.Headers.TryAddWithoutValidation(header, headerValue);
        var provider = Provider(GatewayOptions(), handler);

        var call = () => provider.CompleteAsync(new AiCompletionRequest { Messages = [new(AiRole.User, "Hello.")] });

        var thrown = (await call.Should().ThrowAsync<AiUnavailableException>(story)).Which;
        thrown.Should().BeOfType(expected, story);
        thrown.ErrorCode.Should().Be(code, story);
        thrown.Message.Should().Be(message, story);
        thrown.Message.Should().NotContain("test-gateway-key");
        handler.Requests.Should().HaveCount(1, "a long wait or a refusal is not retried inside the request");
    }

    [Fact]
    public async Task ARateLimitWithAnHttpDate_SaysWhenToTryAgain()
    {
        using var handler = new RecordingHandler((HttpStatusCode.TooManyRequests, """{"error":{"code":"rate_limited"}}"""));
        handler.ResponseHeaders = response => response.Headers.RetryAfter =
            new System.Net.Http.Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(30));
        var provider = Provider(GatewayOptions(), handler);

        var call = () => provider.CompleteAsync(new AiCompletionRequest { Messages = [new(AiRole.User, "Hello.")] });

        var thrown = (await call.Should().ThrowAsync<AiRateLimitedException>()).Which;
        thrown.RetryAfter.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(5));
        thrown.Message.Should().Be("The assistant is busy right now. Try again in 30 minutes.");
    }

    [Fact]
    public async Task AShortRateLimit_IsWaitedOutOnce_InsideTheCall()
    {
        using var handler = new RecordingHandler(
            (HttpStatusCode.TooManyRequests, """{"error":{"code":"rate_limited"}}"""), (HttpStatusCode.OK, TextResponse));
        handler.ResponseHeaders = response =>
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests) response.Headers.TryAddWithoutValidation("Retry-After", "0");
        };
        var provider = Provider(GatewayOptions(), handler);

        var response = await provider.CompleteAsync(new AiCompletionRequest { Messages = [new(AiRole.User, "Hello.")] });

        response.Content.Should().Be("A recorded meal.");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task AShortRateLimit_TwiceInARow_IsRetriedOnlyOnce()
    {
        using var handler = new RecordingHandler((HttpStatusCode.TooManyRequests, """{"error":{"code":"rate_limited"}}"""));
        handler.ResponseHeaders = response => response.Headers.TryAddWithoutValidation("Retry-After", "0");
        var provider = Provider(GatewayOptions(), handler);

        var call = () => provider.CompleteAsync(new AiCompletionRequest { Messages = [new(AiRole.User, "Hello.")] });

        await call.Should().ThrowAsync<AiRateLimitedException>();
        handler.Requests.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("""{"choices":[{"message":{"content":null,"refusal":"No."},"finish_reason":"stop"}]}""")]
    [InlineData("""{"choices":[{"message":{"content":""},"finish_reason":"content_filter"}]}""")]
    public async Task AContentRefusalInAnAnswer_IsBlocked(string body)
    {
        using var handler = new RecordingHandler(body);
        var provider = Provider(GatewayOptions(), handler);

        var call = () => provider.CompleteAsync(new AiCompletionRequest { Messages = [new(AiRole.User, "Hello.")] });

        (await call.Should().ThrowAsync<AiBlockedException>()).Which.Message.Should().Be("I can't help with that.");
    }

    [Theory]
    [InlineData("""{"choices":[{"message":{"content":"Eighteen character"}}],"gateway_extra":{"route":"x"}}""", null, null)]
    [InlineData("""{"choices":[{"message":{"content":"Eighteen character"}}],"usage":{"prompt_tokens":7}}""", 7, null)]
    [InlineData("""{"choices":[{"message":{"content":"Eighteen character"}}],"usage":{"completion_tokens":9}}""", null, 9)]
    public async Task AnAnswerWithMissingOrPartialUsage_IsEstimatedFromItsCharacters(string body, int? reportedPrompt, int? reportedCompletion)
    {
        using var handler = new RecordingHandler(body);
        var provider = Provider(GatewayOptions(), handler);

        var response = await provider.CompleteAsync(new AiCompletionRequest
        {
            Messages = [new(AiRole.System, new string('s', 400)), new(AiRole.User, "Hello there.")],
        });

        response.Content.Should().Be("Eighteen character");
        response.Model.Should().Be("route-alias", "an answer that names no model keeps the configured alias");
        response.Usage.PromptTokens.Should().Be(reportedPrompt ?? (400 + 12) / 4);
        response.Usage.CompletionTokens.Should().Be(reportedCompletion ?? (18 + 3) / 4);
    }

    [Fact]
    public async Task TheKeyGoesOnlyAsBearer_ToAnEndpointThatIsNotAzure()
    {
        using var handler = new RecordingHandler(TextResponse);
        var provider = Provider(GatewayOptions(), handler);

        await provider.CompleteAsync(new AiCompletionRequest { Messages = [new(AiRole.User, "Hello.")] });

        handler.Authorization.Should().Be("Bearer test-gateway-key");
        handler.ApiKey.Should().BeNull();
        handler.Body.GetProperty("model").GetString().Should().Be("route-alias");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task TheEndUserToken_IsSentOnlyWhenEnabled_AndOnlyForAPerson(bool enabled, bool forAPerson)
    {
        using var handler = new RecordingHandler(TextResponse);
        using var tokens = AiEndUserTokensTests.Tokens(enabled ? AiEndUserTokensTests.Options() : new AiEndUserOptions());
        var provider = Provider(GatewayOptions(), handler, identity: tokens);
        var userId = Guid.NewGuid();

        await provider.CompleteAsync(new AiCompletionRequest
        {
            Messages = [new(AiRole.User, "Hello.")],
            EndUserId = forAPerson ? userId : null,
        });

        var headers = handler.Headers.Single();
        if (enabled && forAPerson)
        {
            AiEndUserTokensTests.Claims(headers["X-End-User"]).GetProperty("sub").GetString().Should().Be(userId.ToString("D"));
        }
        else
        {
            headers.Should().NotContainKey("X-End-User");
        }
    }

    [Fact]
    public async Task APhoto_GoesToTheVisionEndpoint_WithoutTheEndUserToken_AndTextStaysOnTheGateway()
    {
        using var handler = new RecordingHandler(TextResponse);
        using var tokens = AiEndUserTokensTests.Tokens(AiEndUserTokensTests.Options());
        var options = GatewayOptions();
        options.Vision = new AiVisionOptions { BaseUrl = "https://vision.test/v1", ApiKey = "test-vision-key", Model = "vision-model" };
        var provider = Provider(options, handler, identity: tokens);

        await provider.CompleteAsync(new AiCompletionRequest
        {
            Messages = [new(AiRole.User, "Identify this.", new AiImage([1, 2, 3], "image/png"))],
            EndUserId = Guid.NewGuid(),
        });
        await provider.CompleteAsync(new AiCompletionRequest
        {
            Messages = [new(AiRole.User, "Hello.")],
            EndUserId = Guid.NewGuid(),
        });

        handler.Requests[0].Should().Be(new Uri("https://vision.test/v1/chat/completions"));
        handler.Bodies[0].GetProperty("model").GetString().Should().Be("vision-model");
        handler.Headers[0]["Authorization"].Should().Be("Bearer test-vision-key");
        handler.Headers[0].Should().NotContainKey("X-End-User");
        handler.Requests[1].Should().Be(new Uri("https://gateway.test/v1/chat/completions"));
        handler.Bodies[1].GetProperty("model").GetString().Should().Be("route-alias");
        handler.Headers[1].Should().ContainKey("X-End-User");
    }

    private static AiOptions AzureOptions(string reasoningEffort) => new()
    {
        BaseUrl = "https://mizan-test.openai.azure.com/openai/v1",
        ApiKey = "test-azure-key",
        Model = "gpt-5.6-luna",
        ReasoningEffort = reasoningEffort,
        SupportsTemperature = false,
    };

    private static OpenAiCompatibleProvider Provider(
        AiOptions options, RecordingHandler handler, RecordingLogger? logger = null, IAiCallIdentity? identity = null)
    {
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(factory => factory.CreateClient(OpenAiCompatibleProvider.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return new OpenAiCompatibleProvider(
            new FixedRuntimeOptions<AiOptions>(options), clients.Object, logger ?? new RecordingLogger(), identity);
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
        public List<Uri> Requests { get; } = [];
        public List<Dictionary<string, string>> Headers { get; } = [];

        /// <summary>Headers on every scripted response, such as Retry-After.</summary>
        public Action<HttpResponseMessage>? ResponseHeaders { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            ApiKey = request.Headers.TryGetValues("api-key", out var keys) ? keys.Single() : null;
            Requests.Add(request.RequestUri!);
            Headers.Add(request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase));
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Body = document.RootElement.Clone();
            Bodies.Add(Body);
            var (status, body) = _responses.Count > 0 ? _responses.Dequeue() : _last;
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            ResponseHeaders?.Invoke(response);
            return response;
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
