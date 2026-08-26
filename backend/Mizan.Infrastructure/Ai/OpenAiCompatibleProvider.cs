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
            Temperature = request.Temperature,
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
        };

        using var client = _clients.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

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

        var content = body.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiUnavailableException("The assistant returned an empty response.");
        }

        var usage = body.Usage is null
            ? AiTokenUsage.None
            : new AiTokenUsage(body.Usage.PromptTokens, body.Usage.CompletionTokens);

        return new AiCompletionResponse(content, usage, body.Model ?? _options.Model);
    }

    private static WireMessage ToWire(AiMessage message)
    {
        var role = message.Role switch
        {
            AiRole.System => "system",
            AiRole.Assistant => "assistant",
            _ => "user",
        };

        if (message.Image is null)
        {
            return new WireMessage { Role = role, Content = message.Content };
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
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
        [JsonPropertyName("response_format")] public ResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class WireMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "user";
        [JsonPropertyName("content")] public object? Content { get; set; }

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
    }

    private sealed class Usage
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
    }
}
