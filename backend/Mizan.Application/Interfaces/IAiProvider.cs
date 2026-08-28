namespace Mizan.Application.Interfaces;

public enum AiRole
{
    System = 0,
    User = 1,
    Assistant = 2,

    /// <summary>The result of a tool call, fed back so the model can carry on.</summary>
    Tool = 3,
}

/// <summary>
/// One turn. Images are base64 data supplied alongside the text.
///
/// An assistant turn that asked for tools carries <see cref="ToolCalls"/>; the
/// tool turns that answer it carry the matching <see cref="ToolCallId"/>.
/// Providers reject a tool result with no call to attach it to, so the pair
/// travels together or not at all.
/// </summary>
public record AiMessage(AiRole Role, string Content, AiImage? Image = null)
{
    public IReadOnlyList<AiToolCall>? ToolCalls { get; init; }
    public string? ToolCallId { get; init; }
}

/// <summary>A tool the model asked for, with its arguments as raw JSON.</summary>
public record AiToolCall(string Id, string Name, string Arguments);

/// <summary>A tool the model is allowed to ask for.</summary>
public record AiToolSpec(string Name, string Description, string ParametersSchema);

public record AiImage(byte[] Bytes, string ContentType);

public record AiCompletionRequest
{
    public IReadOnlyList<AiMessage> Messages { get; init; } = Array.Empty<AiMessage>();

    /// <summary>
    /// When set, the provider is asked for JSON matching this schema and the
    /// response is validated against it. Prose parsing is not a fallback:
    /// a response that fails the schema is a failed call (docs/REFOCUS.md §10).
    /// </summary>
    public AiJsonSchema? ResponseSchema { get; init; }

    /// <summary>
    /// The allowlist for this call. Empty means the model has no tools, which
    /// is the default: a surface that does not need to act does not get the
    /// ability to (docs/REFOCUS.md §10).
    /// </summary>
    public IReadOnlyList<AiToolSpec> Tools { get; init; } = Array.Empty<AiToolSpec>();

    public int? MaxOutputTokens { get; init; }
    public double Temperature { get; init; } = 0.7;
}

public record AiJsonSchema(string Name, string Schema);

public record AiTokenUsage(int PromptTokens, int CompletionTokens)
{
    public static AiTokenUsage None { get; } = new(0, 0);

    public int Total => PromptTokens + CompletionTokens;
}

public record AiCompletionResponse(string Content, AiTokenUsage Usage, string Model)
{
    /// <summary>What the model wants run before it can answer. Empty on an ordinary reply.</summary>
    public IReadOnlyList<AiToolCall> ToolCalls { get; init; } = Array.Empty<AiToolCall>();
}

/// <summary>
/// One OpenAI-compatible chat endpoint. The whole integration surface is
/// configuration - base URL, key, model - so swapping providers never touches
/// a call site (docs/REFOCUS.md §10).
/// </summary>
public interface IAiProvider
{
    /// <summary>The configured model id, for the ledger.</summary>
    string Model { get; }

    /// <summary>False when no endpoint is configured; callers refuse cleanly rather than throwing.</summary>
    bool IsConfigured { get; }

    Task<AiCompletionResponse> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default);
}
