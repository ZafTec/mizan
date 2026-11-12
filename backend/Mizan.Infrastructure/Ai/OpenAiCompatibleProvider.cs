using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// One HTTP client against an OpenAI-compatible /chat/completions endpoint, a provider or a
/// gateway in front of one. Model, endpoint and key are configuration and the model id is opaque,
/// so swapping providers never touches a call site (docs/AI.md).
/// </summary>
public partial class OpenAiCompatibleProvider : IAiProvider
{
    public const string HttpClientName = nameof(OpenAiCompatibleProvider);

    /// <summary>The longest provider message kept for the log and the administrators.</summary>
    public const int MaxProviderMessageLength = 300;

    /// <summary>The longest rate-limit wait that is waited out inside the call, once.</summary>
    public static readonly TimeSpan MaxInlineRetryWait = TimeSpan.FromSeconds(2);

    /// <summary>The most of an error body read: enough for any provider's error object.</summary>
    private const int MaxErrorBodyBytes = 16 * 1024;

    /// <summary>Prompt tokens counted for a photo when the provider reports no usage.</summary>
    private const int EstimatedImageTokens = 800;

    /// <summary>The longest wait or reset read from a provider, so a nonsense value cannot overflow.</summary>
    private const double MaxWaitSeconds = 400 * 86_400;

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IRuntimeOptions<AiOptions> _settings;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<OpenAiCompatibleProvider> _logger;
    private readonly IAiCallIdentity _identity;
    private readonly TimeProvider _time;

    /// <summary>
    /// Endpoint and model pairs that refused tools together with the model's default reasoning.
    /// Tool requests to them send <c>reasoning_effort: none</c> from then on, until the process restarts.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _toolsNeedNoReasoning = new(StringComparer.OrdinalIgnoreCase);

    public OpenAiCompatibleProvider(
        IRuntimeOptions<AiOptions> settings,
        IHttpClientFactory clients,
        ILogger<OpenAiCompatibleProvider> logger,
        IAiCallIdentity? identity = null,
        TimeProvider? time = null)
    {
        _settings = settings;
        _clients = clients;
        _logger = logger;
        _identity = identity ?? NoCallIdentity.Instance;
        _time = time ?? TimeProvider.System;
    }

    public async Task<AiProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken);
        return new AiProviderStatus(IsConfigured(options), options.Model);
    }

    private static bool IsConfigured(AiOptions options) =>
        !string.IsNullOrWhiteSpace(options.BaseUrl) && !string.IsNullOrWhiteSpace(options.ApiKey);

    /// <summary>
    /// Where one request goes. A request with a photo goes to <see cref="AiOptions.Vision"/> when it
    /// names an endpoint, each empty field falling back to the main one (docs/AI.md#photos). The
    /// end-user identity goes only to the main endpoint: it is meant for the gateway.
    /// </summary>
    private sealed record Endpoint(string BaseUrl, string ApiKey, string Model, bool SendsIdentity);

    private static Endpoint EndpointFor(AiOptions options, AiCompletionRequest request)
    {
        var main = new Endpoint(options.BaseUrl.Trim().TrimEnd('/'), options.ApiKey, options.Model, SendsIdentity: true);
        var vision = options.Vision ?? new AiVisionOptions();
        if (!request.HasImage || string.IsNullOrWhiteSpace(vision.BaseUrl)) return main;

        var baseUrl = vision.BaseUrl.Trim().TrimEnd('/');
        return new Endpoint(
            baseUrl,
            string.IsNullOrWhiteSpace(vision.ApiKey) ? options.ApiKey : vision.ApiKey,
            string.IsNullOrWhiteSpace(vision.Model) ? options.Model : vision.Model.Trim(),
            SendsIdentity: string.Equals(baseUrl, main.BaseUrl, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AiCompletionResponse> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Read per call: an endpoint, key or model changed in the admin area applies to the next call.
        var options = await _settings.GetAsync(cancellationToken);
        if (!IsConfigured(options))
        {
            throw new AiUnavailableException("The assistant is not configured on this server.", "not_configured");
        }

        var endpoint = EndpointFor(options, request);
        var payload = new ChatCompletionRequest
        {
            Model = endpoint.Model,
            MaxCompletionTokens = request.MaxOutputTokens ?? options.MaxOutputTokens,
            Temperature = options.SupportsTemperature ? request.Temperature : null,
            ReasoningEffort = string.IsNullOrWhiteSpace(options.ReasoningEffort)
                ? null
                : options.ReasoningEffort.Trim(),
            Messages = request.Messages.Select(ToWire).ToList(),
            ResponseFormat = request.ResponseSchema is { } schema
                ? new ResponseFormat
                {
                    Type = "json_schema",
                    JsonSchema = new JsonSchemaSpec
                    {
                        Name = schema.Name,
                        Strict = true,
                        Schema = JsonElement.Parse(schema.Schema),
                    },
                }
                : null,
            Tools = request.Tools.Count == 0
                ? null
                : request.Tools.Select(tool => new WireTool
                {
                    Function = new WireFunction
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        Parameters = JsonElement.Parse(tool.ParametersSchema),
                    },
                }).ToList(),
        };

        var pairKey = endpoint.BaseUrl + "|" + endpoint.Model;
        if (payload.Tools is not null && _toolsNeedNoReasoning.ContainsKey(pairKey)) payload.ReasoningEffort = "none";

        using var client = _clients.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(endpoint.BaseUrl + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        var identity = endpoint.SendsIdentity ? await _identity.HeadersForAsync(request.EndUserId, cancellationToken) : [];
        using var response = await SendAsync(client, payload, endpoint, identity, pairKey, cancellationToken);

        // Members this client does not know are ignored, so a gateway may add its own.
        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(Json, cancellationToken)
            ?? throw new AiUnavailableException("The assistant returned an empty response.", "empty_response");

        var first = body.Choices?.FirstOrDefault();
        var choice = first?.Message;
        var content = choice?.Content;
        var toolCalls = choice?.ToolCalls?
            .Where(call => call.Function is not null)
            .Select(call => new AiToolCall(
                call.Id ?? string.Empty, call.Function!.Name ?? string.Empty, call.Function.Arguments ?? "{}"))
            .ToList() ?? [];

        // A turn that only asks for tools has no prose, and that is a valid
        // answer rather than an empty one.
        if (string.IsNullOrWhiteSpace(content) && toolCalls.Count == 0)
        {
            // The model or the provider's filter declined the content: the same words get the same answer.
            if (!string.IsNullOrWhiteSpace(choice?.Refusal)
                || string.Equals(first?.FinishReason, "content_filter", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("AI provider declined the content of a request");
                throw new AiBlockedException();
            }

            throw new AiUnavailableException("The assistant returned an empty response.", "empty_response");
        }

        return new AiCompletionResponse(
            content ?? string.Empty, UsageOf(body.Usage, request, content, toolCalls), body.Model ?? endpoint.Model)
        {
            ToolCalls = toolCalls,
        };
    }

    /// <summary>
    /// The provider's token counts. A count it leaves out is estimated at four characters a token
    /// (a photo at a flat <see cref="EstimatedImageTokens"/>), so the allowance and the cost ceiling
    /// still see the call.
    /// </summary>
    private static AiTokenUsage UsageOf(Usage? reported, AiCompletionRequest request, string? content, IReadOnlyList<AiToolCall> toolCalls)
    {
        var prompt = reported?.PromptTokens;
        if (prompt is null)
        {
            var characters = request.Messages.Sum(m => m.Content?.Length ?? 0)
                + request.Tools.Sum(t => t.Name.Length + t.Description.Length + t.ParametersSchema.Length);
            prompt = characters / 4 + request.Messages.Count(m => m.Image is not null) * EstimatedImageTokens;
        }

        var completion = reported?.CompletionTokens
            ?? ((content?.Length ?? 0) + toolCalls.Sum(c => c.Name.Length + c.Arguments.Length) + 3) / 4;
        return new AiTokenUsage(Math.Max(0, prompt.Value), Math.Max(0, completion));
    }

    /// <summary>
    /// Posts the request. A non-success status is logged with the provider's error type, code,
    /// param and message (cut short, secrets removed, never the request or the end-user token) and
    /// becomes a typed refusal (limit, rate, blocked) or an <see cref="AiProviderRejectedException"/>;
    /// the caller never receives the body. Two refusals are retried, once each: a rate limit whose
    /// wait is at most <see cref="MaxInlineRetryWait"/>, and a tool request refused because of
    /// reasoning_effort, which goes again with <c>reasoning_effort: none</c> (docs/AI.md#provider-errors).
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        ChatCompletionRequest payload,
        Endpoint endpoint,
        IReadOnlyList<KeyValuePair<string, string>> identity,
        string pairKey,
        CancellationToken cancellationToken)
    {
        var waitedForRate = false;
        while (true)
        {
            HttpResponseMessage response;
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = JsonContent.Create(payload, options: Json),
                };
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ApiKey);
                // Azure OpenAI's key-based auth ignores Bearer (that slot is reserved for Entra ID
                // tokens) and reads this header instead. Only an Azure host gets it, so the key is
                // not handed to a gateway or another provider a second time.
                if (IsAzureHost(endpoint.BaseUrl)) message.Headers.Add("api-key", endpoint.ApiKey);
                foreach (var (name, value) in identity) message.Headers.TryAddWithoutValidation(name, value);

                response = await client.SendAsync(message, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiUnavailableException("The assistant took too long to answer. Try again.", "timeout");
            }
            catch (HttpRequestException)
            {
                throw new AiUnavailableException("The assistant could not be reached. Try again shortly.", "unreachable");
            }

            if (response.IsSuccessStatusCode) return response;

            var status = (int)response.StatusCode;
            var now = _time.GetUtcNow();
            ProviderError error;
            TimeSpan? wait;
            using (response)
            {
                error = await ReadErrorAsync(response, endpoint.ApiKey, now, cancellationToken);
                wait = RetryAfterOf(response.Headers, now) ?? error.RetryAfter;
            }

            _logger.LogWarning(
                "AI provider refused the request with {Status}: type {ProviderType}, code {ProviderCode}, param {ProviderParam}, message {ProviderMessage}",
                status, error.Type, error.Code, error.Param, error.Message);

            if (status == 429 && !waitedForRate && wait is { } shortWait && shortWait <= MaxInlineRetryWait)
            {
                waitedForRate = true;
                if (shortWait > TimeSpan.Zero) await Task.Delay(shortWait, _time, cancellationToken);
                continue;
            }

            if (status == 400 && payload.Tools is not null && payload.ReasoningEffort != "none" && RefusesReasoningWithTools(error))
            {
                _toolsNeedNoReasoning[pairKey] = true;
                _logger.LogWarning(
                    "AI model {Model} refuses tools with its default reasoning; retrying with reasoning_effort none. Set Ai.ReasoningEffort to none under Admin, Settings to avoid the first refusal.",
                    endpoint.Model);
                payload.ReasoningEffort = "none";
                continue;
            }

            throw Refusal(status, error, wait, now);
        }
    }

    /// <summary>
    /// A refusal the caller can act on: 402 is a usage or budget stop, 429 a rate limit, a content
    /// refusal is blocked, and a blocked person is told so. Anything else keeps the status mapping
    /// of <see cref="AiProviderRejectedException"/>, so a wrong end-user token (401) is still a
    /// misconfiguration the administrators hear about.
    /// </summary>
    private static AiUnavailableException Refusal(int status, ProviderError error, TimeSpan? wait, DateTimeOffset now)
    {
        if (status == 402)
            return new AiLimitReachedException((error.ResetsAt ?? (wait is { } until ? now + until : null))?.UtcDateTime);
        if (status == 429) return new AiRateLimitedException(wait);
        if (status == 403 && IsCode(error, "end_user_blocked")) return new AiAccountBlockedException();
        if (status is 400 or 403 or 422 && IsCode(error, "guardrail_blocked", "content_filter", "content_policy_violation"))
            return new AiBlockedException();
        return new AiProviderRejectedException(status, error.Type, error.Code, error.Param, error.Message);
    }

    private static bool IsCode(ProviderError error, params string[] codes) =>
        codes.Any(code => string.Equals(error.Code, code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(error.Type, code, StringComparison.OrdinalIgnoreCase));

    private static bool IsAzureHost(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
        && uri.Host.EndsWith(".azure.com", StringComparison.OrdinalIgnoreCase);

    private static bool RefusesReasoningWithTools(ProviderError error) =>
        string.Equals(error.Param, "reasoning_effort", StringComparison.OrdinalIgnoreCase)
        || (error.Message?.Contains("reasoning_effort", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>
    /// How long to wait: <c>Retry-After</c> in seconds or as an HTTP date, else the reset of an
    /// exhausted policy in the IETF <c>RateLimit</c> (or <c>RateLimit-Reset</c>) header.
    /// </summary>
    internal static TimeSpan? RetryAfterOf(HttpResponseHeaders headers, DateTimeOffset now)
    {
        if (headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta) return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
            if (retryAfter.Date is { } date) return date <= now ? TimeSpan.Zero : date - now;
        }

        var values = new List<string>();
        if (headers.TryGetValues("RateLimit", out var rateLimit)) values.AddRange(rateLimit);
        if (headers.TryGetValues("RateLimit-Reset", out var reset)) values.AddRange(reset.Select(v => "reset=" + v.Trim()));
        return values.Count == 0 ? null : RateLimitReset(values);
    }

    /// <summary>
    /// Reads both shapes of the header: <c>limit=10, remaining=0, reset=30</c> and the structured
    /// <c>"day";r=0;t=30, "week";r=5;t=600000</c>. The longest reset of a policy with nothing left
    /// wins; with no such policy, the longest reset given.
    /// </summary>
    internal static TimeSpan? RateLimitReset(IEnumerable<string> values)
    {
        double? exhausted = null, any = null;
        foreach (var item in values.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            double? itemReset = null, itemRemaining = null;
            foreach (var part in item.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var equals = part.IndexOf('=');
                if (equals <= 0) continue;
                var key = part[..equals].Trim().ToLowerInvariant();
                if (!double.TryParse(part[(equals + 1)..].Trim().Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    || number < 0 || number > MaxWaitSeconds) continue;
                if (key is "t" or "reset") itemReset = number;
                else if (key is "r" or "remaining") itemRemaining = number;
            }

            if (itemReset is not { } seconds) continue;
            any = Math.Max(any ?? 0, seconds);
            if (itemRemaining == 0) exhausted = Math.Max(exhausted ?? 0, seconds);
        }

        return (exhausted ?? any) is { } wait ? TimeSpan.FromSeconds(wait) : null;
    }

    /// <summary>The provider's error object, as much of it as is read. A body that is not JSON keeps no message: it may quote the request.</summary>
    private static async Task<ProviderError> ReadErrorAsync(
        HttpResponseMessage response, string apiKey, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[MaxErrorBodyBytes];
            var read = 0;
            int chunk;
            while (read < buffer.Length && (chunk = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken)) > 0) read += chunk;

            using var document = JsonDocument.Parse(buffer.AsMemory(0, read));
            var root = document.RootElement;
            var error = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var inner) ? inner : root;
            if (error.ValueKind == JsonValueKind.String)
                return new ProviderError(null, null, null, Clean(error.GetString(), apiKey));
            if (error.ValueKind != JsonValueKind.Object) return ProviderError.None;
            return new ProviderError(
                Clean(Text(error, "type"), apiKey),
                Clean(Text(error, "code"), apiKey),
                Clean(Text(error, "param"), apiKey),
                Clean(Text(error, "message"), apiKey))
            {
                ResetsAt = ResetOf(error, now),
                RetryAfter = SecondsOf(error, "retry_after"),
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException or HttpRequestException)
        {
            return ProviderError.None;
        }
    }

    /// <summary>When a limit lifts, from <c>resets_at</c> or <c>reset_at</c> (a date or Unix seconds) or <c>retry_after</c> in the error object.</summary>
    private static DateTimeOffset? ResetOf(JsonElement error, DateTimeOffset now)
    {
        foreach (var name in new[] { "resets_at", "reset_at" })
        {
            if (!error.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(
                    value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
                return at;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unix) && unix > 0 && unix < 32_503_680_000)
                return DateTimeOffset.FromUnixTimeSeconds(unix);
        }

        return SecondsOf(error, "retry_after") is { } wait ? now + wait : null;
    }

    private static TimeSpan? SecondsOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var seconds) && seconds is >= 0 and <= MaxWaitSeconds
            ? TimeSpan.FromSeconds(seconds)
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;

    /// <summary>
    /// One line of at most <see cref="MaxProviderMessageLength"/> characters, with the configured
    /// key and anything shaped like a key, a bearer token or a JWT replaced.
    /// </summary>
    internal static string? Clean(string? value, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (!string.IsNullOrEmpty(apiKey) && apiKey.Length >= 4) text = text.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
        text = SecretLike().Replace(text, "[redacted]");
        return text.Length <= MaxProviderMessageLength ? text : text[..MaxProviderMessageLength] + "…";
    }

    [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9._~+/=-]+|\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*|\bsk-[A-Za-z0-9_-]{8,}|\b[A-Za-z0-9]{32,}\b")]
    private static partial Regex SecretLike();

    private sealed record ProviderError(string? Type, string? Code, string? Param, string? Message)
    {
        public static readonly ProviderError None = new(null, null, null, null);

        /// <summary>When a usage stop lifts, if the error object says.</summary>
        public DateTimeOffset? ResetsAt { get; init; }

        /// <summary>A <c>retry_after</c> in seconds in the error object, used when no header gives one.</summary>
        public TimeSpan? RetryAfter { get; init; }
    }

    /// <summary>No end-user identity: no extra header.</summary>
    private sealed class NoCallIdentity : IAiCallIdentity
    {
        public static readonly NoCallIdentity Instance = new();

        public ValueTask<IReadOnlyList<KeyValuePair<string, string>>> HeadersForAsync(Guid? endUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<KeyValuePair<string, string>>>([]);
    }

    private static WireMessage ToWire(AiMessage message)
    {
        var role = message.Role switch
        {
            AiRole.System => "system",
            AiRole.Assistant => "assistant",
            AiRole.Tool => "tool",
            _ => "user",
        };

        if (message.ToolCalls is { Count: > 0 })
        {
            return new WireMessage
            {
                Role = role,
                Content = string.IsNullOrEmpty(message.Content) ? null : message.Content,
                ToolCalls = message.ToolCalls.Select(call => new WireToolCall
                {
                    Id = call.Id,
                    Function = new WireToolCallFunction { Name = call.Name, Arguments = call.Arguments },
                }).ToList(),
            };
        }

        if (message.Image is null)
        {
            return new WireMessage
            {
                Role = role,
                Content = message.Content,
                ToolCallId = message.ToolCallId,
            };
        }

        var dataUrl = DataUrl(message.Image);
        return new WireMessage
        {
            Role = role,
            ContentParts =
            [
                new ContentPart { Type = "text", Text = message.Content },
                new ContentPart { Type = "image_url", ImageUrl = new ImageUrl { Url = dataUrl } },
            ],
        };
    }

    /// <summary>
    /// The photo as a <c>data:</c> URL, encoded straight into the one string: a photo runs to megabytes,
    /// so encoding it to a string and then copying that into the URL would allocate it twice.
    /// </summary>
    private static string DataUrl(AiImage image)
    {
        var prefix = $"data:{image.ContentType};base64,";
        var length = prefix.Length + checked((image.Bytes.Length + 2) / 3 * 4);
        return string.Create(length, (prefix, image.Bytes), static (span, state) =>
        {
            state.prefix.CopyTo(span);
            Convert.TryToBase64Chars(state.Bytes, span[state.prefix.Length..], out _);
        });
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public List<WireMessage> Messages { get; set; } = new();
        [JsonPropertyName("max_completion_tokens")] public int MaxCompletionTokens { get; set; }
        [JsonPropertyName("temperature")] public double? Temperature { get; set; }
        [JsonPropertyName("reasoning_effort")] public string? ReasoningEffort { get; set; }
        [JsonPropertyName("response_format")] public ResponseFormat? ResponseFormat { get; set; }
        [JsonPropertyName("tools")] public List<WireTool>? Tools { get; set; }
    }

    private sealed class WireTool
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "function";
        [JsonPropertyName("function")] public WireFunction? Function { get; set; }
    }

    private sealed class WireFunction
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("parameters")] public JsonElement Parameters { get; set; }
    }

    private sealed class WireToolCall
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("type")] public string Type { get; set; } = "function";
        [JsonPropertyName("function")] public WireToolCallFunction? Function { get; set; }
    }

    private sealed class WireToolCallFunction
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("arguments")] public string? Arguments { get; set; }
    }

    private sealed class WireMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "user";
        [JsonPropertyName("content")] public object? Content { get; set; }

        [JsonPropertyName("tool_calls")] public List<WireToolCall>? ToolCalls { get; set; }
        [JsonPropertyName("tool_call_id")] public string? ToolCallId { get; set; }

        [JsonIgnore]
        public List<ContentPart>? ContentParts
        {
            get => Content as List<ContentPart>;
            set => Content = value;
        }
    }

    private sealed class ContentPart
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "text";
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("image_url")] public ImageUrl? ImageUrl { get; set; }
    }

    private sealed class ImageUrl
    {
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    }

    private sealed class ResponseFormat
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "json_schema";
        [JsonPropertyName("json_schema")] public JsonSchemaSpec? JsonSchema { get; set; }
    }

    private sealed class JsonSchemaSpec
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("strict")] public bool Strict { get; set; }
        [JsonPropertyName("schema")] public JsonElement Schema { get; set; }
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
        [JsonPropertyName("usage")] public Usage? Usage { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public ChoiceMessage? Message { get; set; }
        [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
    }

    private sealed class ChoiceMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("refusal")] public string? Refusal { get; set; }
        [JsonPropertyName("tool_calls")] public List<WireToolCall>? ToolCalls { get; set; }
    }

    private sealed class Usage
    {
        [JsonPropertyName("prompt_tokens")] public int? PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int? CompletionTokens { get; set; }
    }
}
