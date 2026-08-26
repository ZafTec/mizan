namespace Mizan.Application.Interfaces;

public enum AiRole
{
    System = 0,
    User = 1,
    Assistant = 2,
}

/// <summary>One turn. Images are base64 data supplied alongside the text.</summary>
public record AiMessage(AiRole Role, string Content, AiImage? Image = null);

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

    public int? MaxOutputTokens { get; init; }
    public double Temperature { get; init; } = 0.7;
}

public record AiJsonSchema(string Name, string Schema);

public record AiTokenUsage(int PromptTokens, int CompletionTokens)
{
    public static AiTokenUsage None { get; } = new(0, 0);

    public int Total => PromptTokens + CompletionTokens;
}

public record AiCompletionResponse(string Content, AiTokenUsage Usage, string Model);

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
