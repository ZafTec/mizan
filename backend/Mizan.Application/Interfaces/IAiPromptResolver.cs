namespace Mizan.Application.Interfaces;

/// <summary>
/// The composed system prompt and the version that produced it. The id goes on
/// the usage row so an answer can be traced back to its exact text.
/// </summary>
public record ResolvedPrompt(Guid? VersionId, int? Version, string SystemPrompt);

public interface IAiPromptResolver
{
    /// <summary>
    /// The published version for a key, or the built-in default when nothing is
    /// published. A fresh database must not mean a mute assistant.
    /// </summary>
    Task<ResolvedPrompt> ResolveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Composes a specific body and soft policy without publishing it - used by evals.</summary>
    string Compose(string body, string softPolicyJson);
}
