namespace Mizan.Domain.Entities;

public enum AiEvalOutcome
{
    Passed = 0,
    Failed = 1,
    Errored = 2,
}

/// <summary>
/// A fixture a draft is run against before it can be published.
///
/// Inputs are synthetic, always. An admin has operational access, not
/// super-user access over personal data (docs/REFOCUS.md §11), and tuning a
/// prompt against real logs is exactly how that line gets crossed by accident.
/// There is no code path here that reads a user's data.
/// </summary>
public class AiEvalCase
{
    public Guid Id { get; set; }
    public string PromptKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>The synthetic user turn.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>Optional synthetic context, standing in for a consented log summary.</summary>
    public string? Context { get; set; }

    /// <summary>JSON assertions: mustContain, mustNotContain, requireSchema.</summary>
    public string Assertions { get; set; } = "{}";

    /// <summary>
    /// Prompt injection, requests for another user's data, medical-advice bait.
    /// A draft that fails one of these does not get a publish button.
    /// </summary>
    public bool IsAdversarial { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class AiEvalRun
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public Guid CaseId { get; set; }
    public AiEvalOutcome Outcome { get; set; }
    public bool SchemaValid { get; set; }
    public string? Output { get; set; }
    public string? FailureReason { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public long CostMicros { get; set; }
    public int LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual AiPromptVersion? Version { get; set; }
    public virtual AiEvalCase? Case { get; set; }
}
