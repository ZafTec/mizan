namespace Mizan.Domain.Entities;

/// <summary>
/// A high-impact tool action that was previewed and waits for the person to confirm it
/// (docs/MCP.md#confirming-a-high-impact-action). The caller holds a one-time token; only its
/// hash is kept. Confirming rebuilds the request from the stored arguments, previews it again
/// and refuses when what it would remove has changed.
/// </summary>
public class PendingToolAction
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The connection the preview came through; null for the web app's own session.</summary>
    public Guid? GrantId { get; set; }

    public string Tool { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;

    /// <summary>The arguments as sent, from which the request is built again on confirmation.</summary>
    public string ArgumentsJson { get; set; } = "{}";

    /// <summary>SHA-256 of <see cref="ArgumentsJson"/>, so a stored row that was altered is refused.</summary>
    public string ArgumentsHash { get; set; } = string.Empty;

    /// <summary>SHA-256 of what the preview showed. Confirming refuses when the preview no longer says the same.</summary>
    public string FactsHash { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
