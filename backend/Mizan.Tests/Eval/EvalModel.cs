using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Mizan.Tests.Eval;

/// <summary>One answer from the model: its message as it goes back into the conversation, and what it cost.</summary>
public sealed record ModelReply(JsonObject Message, string? Content, IReadOnlyList<ModelToolCall> ToolCalls, int PromptTokens, int CompletionTokens);

public sealed record ModelToolCall(string Id, string Name, string Arguments);

/// <summary>
/// An OpenAI-compatible chat-completions endpoint with tool calling. The address and key come from the
/// environment and never appear in a message this class writes: an error names the status and the
/// provider's message only, with the key and the address removed.
/// </summary>
public sealed class EvalModel : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _key;
    private readonly string _baseUrl;
    private readonly int _maxTokens;

    private EvalModel(string baseUrl, string key, string model, int maxTokens)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _key = key;
        _model = model;
        _maxTokens = maxTokens;
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    public string Model => _model;

    public static EvalModel FromEnvironment()
    {
        string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is not set.");
        var maxTokens = int.TryParse(Environment.GetEnvironmentVariable("EVAL_MAX_TOKENS"), out var parsed) ? parsed : 4000;
        return new EvalModel(Required("EVAL_BASE_URL"), Required("EVAL_KEY"), Required("EVAL_MODEL"), maxTokens);
    }

    public async Task<ModelReply> CompleteAsync(JsonArray messages, JsonArray tools, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["model"] = _model,
            ["messages"] = messages.DeepClone(),
            ["tools"] = tools.DeepClone(),
            ["tool_choice"] = "auto",
            ["max_tokens"] = _maxTokens,
        };

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/chat/completions")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested && attempt < 6)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cancellationToken);
                continue;
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    if (attempt < 6)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10 * attempt), cancellationToken);
                        continue;
                    }
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"The model answered {(int)response.StatusCode}: {Scrub(text)}");
                }
                return Parse(text);
            }
        }
    }

    private static ModelReply Parse(string text)
    {
        var root = JsonNode.Parse(text)!.AsObject();
        var message = root["choices"]![0]!["message"]!.AsObject();
        var content = message["content"]?.GetValue<string>();
        var calls = new List<ModelToolCall>();
        if (message["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var call in toolCalls.OfType<JsonObject>())
            {
                calls.Add(new ModelToolCall(
                    call["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                    call["function"]?["name"]?.GetValue<string>() ?? string.Empty,
                    call["function"]?["arguments"]?.GetValue<string>() ?? "{}"));
            }
        }

        // What goes back into the conversation: the role, the text and the calls, nothing provider-specific.
        var kept = new JsonObject { ["role"] = "assistant", ["content"] = content };
        if (calls.Count > 0)
        {
            kept["tool_calls"] = new JsonArray(calls.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Arguments },
            }).ToArray());
        }

        var usage = root["usage"];
        return new ModelReply(
            kept,
            content,
            calls,
            usage?["prompt_tokens"]?.GetValue<int>() ?? 0,
            usage?["completion_tokens"]?.GetValue<int>() ?? 0);
    }

    private string Scrub(string text)
    {
        var scrubbed = text.Replace(_key, "[key]", StringComparison.Ordinal).Replace(_baseUrl, "[endpoint]", StringComparison.OrdinalIgnoreCase);
        return scrubbed.Length > 400 ? scrubbed[..400] : scrubbed;
    }

    public void Dispose() => _http.Dispose();
}
