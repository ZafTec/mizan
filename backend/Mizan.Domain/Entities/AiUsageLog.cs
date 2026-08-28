namespace Mizan.Domain.Entities;

public enum AiCallOutcome
{
    /// <summary>Reserved but not yet settled. Counts against quota while in flight.</summary>
    Pending = 4,

    Succeeded = 0,
    ProviderError = 1,
    Timeout = 2,
    InvalidResponse = 3,
}

/// <summary>
/// The durable ledger of every model call. Source of truth for the usage tab,
/// for the global spend ceiling, and for reconciling against the provider's
/// invoice. Redis counters are a cache in front of this and are rebuildable
/// from it - see docs/REFOCUS.md §10.
///
/// A row is written whether the call succeeded or not: a timeout still costs
/// tokens at the provider, and a failure rate is exactly what you want to see
/// when the bill looks wrong.
/// </summary>
public class AiUsageLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Active household at the time of the call; context is scoped to it (§11).</summary>
    public Guid? HouseholdId { get; set; }

    /// <summary>Which surface spent this - "chat", "food-analysis", "onboarding".</summary>
    public string Feature { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }

    /// <summary>Millionths of a currency unit. Integers, because money and doubles do not mix.</summary>
    public long EstimatedCostMicros { get; set; }

    /// <summary>
    /// The exact prompt version that produced this answer, so a quality
    /// regression is bisectable instead of mysterious (docs/REFOCUS.md §12).
    /// </summary>
    public Guid? PromptVersionId { get; set; }

    public int LatencyMs { get; set; }
    public AiCallOutcome Outcome { get; set; }
    public DateTime CreatedAt { get; set; }

    public int TotalTokens => PromptTokens + CompletionTokens;

    public virtual User? User { get; set; }
}
