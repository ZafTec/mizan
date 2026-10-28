using System.Collections.Concurrent;
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
/// One HTTP client against an OpenAI-compatible /chat/completions endpoint.
/// Model, endpoint and key are configuration, so swapping providers never
/// touches a call site (docs/AI.md).
/// </summary>
public partial class OpenAiCompatibleProvider : IAiProvider
{
    public const string HttpClientName = nameof(OpenAiCompatibleProvider);

    /// <summary>The longest provider message kept for the log and the administrators.</summary>
    public const int MaxProviderMessageLength = 300;

    /// <summary>The most of an error body read: enough for any provider's error object.</summary>
    private const int MaxErrorBodyBytes = 16 * 1024;

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IRuntimeOptions<AiOptions> _settings;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<OpenAiCompatibleProvider> _logger;

    /// <summary>
    /// Endpoint and model pairs that refused tools together with the model's default reasoning.
    /// Tool requests to them send <c>reasoning_effort: none</c> from then on, until the process restarts.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _toolsNeedNoReasoning = new(StringComparer.OrdinalIgnoreCase);

    public OpenAiCompatibleProvider(
        IRuntimeOptions<AiOptions> settings, IHttpClientFactory clients, ILogger<OpenAiCompatibleProvider> logger)
    {
        _settings = settings;
        _clients = clients;
        _logger = logger;
    }

    public async Task<AiProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken);
        return new AiProviderStatus(IsConfigured(options), options.Model);
    }

    private static bool IsConfigured(AiOptions options) =>
        !string.IsNullOrWhiteSpace(options.BaseUrl) && !string.IsNullOrWhiteSpace(options.ApiKey);

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

        var payload = new ChatCompletionRequest
        {
            Model = options.Model,
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
                        Schema = JsonDocument.Parse(schema.Schema).RootElement.Clone(),
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
                        Parameters = JsonDocument.Parse(tool.ParametersSchema).RootElement.Clone(),
                    },
                }).ToList(),
        };

        var pairKey = options.BaseUrl.TrimEnd('/') + "|" + options.Model;
        if (payload.Tools is not null && _toolsNeedNoReasoning.ContainsKey(pairKey)) payload.ReasoningEffort = "none";

        using var client = _clients.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        // Azure OpenAI's key-based auth ignores Bearer (that slot is reserved for
        // Entra ID tokens) and reads this header instead; every other
        // OpenAI-compatible provider just ignores it.
        client.DefaultRequestHeaders.Add("api-key", options.ApiKey);

        using var response = await SendAsync(client, payload, options, pairKey, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(Json, cancellationToken)
            ?? throw new AiUnavailableException("The assistant returned an empty response.", "empty_response");

        var choice = body.Choices?.FirstOrDefault()?.Message;
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
            throw new AiUnavailableException("The assistant returned an empty response.", "empty_response");
        }

        var usage = body.Usage is null
            ? AiTokenUsage.None
            : new AiTokenUsage(body.Usage.PromptTokens, body.Usage.CompletionTokens);

        return new AiCompletionResponse(content ?? string.Empty, usage, body.Model ?? options.Model)
        {
            ToolCalls = toolCalls,
        };
    }

    /// <summary>
    /// Posts the request. A non-success status is logged with the provider's error type, code,
    /// param and message (cut short, secrets removed, never the request) and becomes an
    /// <see cref="AiProviderRejectedException"/>; the caller never receives the body. One refusal
    /// is retried: a tool request refused because of reasoning_effort (Azure reasoning models on
    /// chat completions) goes again once with <c>reasoning_effort: none</c> (docs/AI.md#provider-errors).
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        HttpClient client, ChatCompletionRequest payload, AiOptions options, string pairKey, CancellationToken cancellationToken)
    {
        while (true)
        {
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync("chat/completions", payload, Json, cancellationToken);
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
            ProviderError error;
            using (response)
            {
                error = await ReadErrorAsync(response, options.ApiKey, cancellationToken);
            }

            _logger.LogWarning(
                "AI provider refused the request with {Status}: type {ProviderType}, code {ProviderCode}, param {ProviderParam}, message {ProviderMessage}",
                status, error.Type, error.Code, error.Param, error.Message);

            if (status == 400 && payload.Tools is not null && payload.ReasoningEffort != "none" && RefusesReasoningWithTools(error))
            {
                _toolsNeedNoReasoning[pairKey] = true;
                _logger.LogWarning(
                    "AI model {Model} refuses tools with its default reasoning; retrying with reasoning_effort none. Set Ai.ReasoningEffort to none under Admin, Settings to avoid the first refusal.",
                    options.Model);
                payload.ReasoningEffort = "none";
                continue;
            }

            throw new AiProviderRejectedException(status, error.Type, error.Code, error.Param, error.Message);
        }
    }

    private static bool RefusesReasoningWithTools(ProviderError error) =>
        string.Equals(error.Param, "reasoning_effort", StringComparison.OrdinalIgnoreCase)
        || (error.Message?.Contains("reasoning_effort", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>The provider's error object, as much of it as is read. A body that is not JSON keeps no message: it may quote the request.</summary>
    private static async Task<ProviderError> ReadErrorAsync(HttpResponseMessage response, string apiKey, CancellationToken cancellationToken)
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
                Clean(Text(error, "message"), apiKey));
        }
        catch (Exception ex) when (ex is JsonException or IOException or HttpRequestException)
        {
            return ProviderError.None;
        }
    }

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
    /// key and anything shaped like a key or a bearer token replaced.
    /// </summary>
    internal static string? Clean(string? value, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (!string.IsNullOrEmpty(apiKey) && apiKey.Length >= 4) text = text.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
        text = SecretLike().Replace(text, "[redacted]");
        return text.Length <= MaxProviderMessageLength ? text : text[..MaxProviderMessageLength] + "…";
    }

    [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9._~+/=-]+|\bsk-[A-Za-z0-9_-]{8,}|\b[A-Za-z0-9]{32,}\b")]
    private static partial Regex SecretLike();

    private sealed record ProviderError(string? Type, string? Code, string? Param, string? Message)
    {
        public static readonly ProviderError None = new(null, null, null, null);
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

        var dataUrl = $"data:{message.Image.ContentType};base64,{Convert.ToBase64String(message.Image.Bytes)}";
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
    }

    private sealed class ChoiceMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("tool_calls")] public List<WireToolCall>? ToolCalls { get; set; }
    }

    private sealed class Usage
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
    }
}
