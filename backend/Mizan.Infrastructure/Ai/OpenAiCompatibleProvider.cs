using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// One HTTP client against an OpenAI-compatible /chat/completions endpoint.
/// Model, endpoint and key are configuration, so swapping providers never
/// touches a call site (docs/REFOCUS.md §10).
/// </summary>
public class OpenAiCompatibleProvider : IAiProvider
{
    public const string HttpClientName = nameof(OpenAiCompatibleProvider);

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AiOptions _options;
    private readonly IHttpClientFactory _clients;

    public OpenAiCompatibleProvider(IOptions<AiOptions> options, IHttpClientFactory clients)
    {
        _options = options.Value;
        _clients = clients;
    }

    public string Model => _options.Model;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.BaseUrl) && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<AiCompletionResponse> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new AiUnavailableException("The assistant is not configured on this server.");
        }

        var payload = new ChatCompletionRequest
        {
            Model = _options.Model,
            MaxCompletionTokens = request.MaxOutputTokens ?? _options.MaxOutputTokens,
            Temperature = _options.SupportsTemperature ? request.Temperature : null,
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

        using var client = _clients.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        // Azure OpenAI's key-based auth ignores Bearer (that slot is reserved for
        // Entra ID tokens) and reads this header instead; every other
        // OpenAI-compatible provider just ignores it.
        client.DefaultRequestHeaders.Add("api-key", _options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("chat/completions", payload, Json, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiUnavailableException("The assistant took too long to answer. Try again.");
        }
        catch (HttpRequestException)
        {
            throw new AiUnavailableException("The assistant could not be reached. Try again shortly.");
        }

        if (!response.IsSuccessStatusCode)
        {
            // The provider's own body can quote the prompt back, so it is not
            // relayed to the caller. The status is enough to act on.
            throw new AiUnavailableException(
                $"The assistant returned an error ({(int)response.StatusCode}). Try again shortly.");
        }

        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(Json, cancellationToken)
            ?? throw new AiUnavailableException("The assistant returned an empty response.");

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
            throw new AiUnavailableException("The assistant returned an empty response.");
        }

        var usage = body.Usage is null
            ? AiTokenUsage.None
            : new AiTokenUsage(body.Usage.PromptTokens, body.Usage.CompletionTokens);

        return new AiCompletionResponse(content ?? string.Empty, usage, body.Model ?? _options.Model)
        {
            ToolCalls = toolCalls,
        };
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
