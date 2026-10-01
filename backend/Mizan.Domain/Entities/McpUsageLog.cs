namespace Mizan.Domain.Entities;

public class McpUsageLog
{
    public Guid Id { get; set; }
    /// <summary>The old personal token, until personal tokens are removed. Null for OAuth connections.</summary>
    public Guid? McpTokenId { get; set; }

    /// <summary>The OAuth connection that made the call.</summary>
    public Guid? GrantId { get; set; }

    /// <summary>tool, resource, prompt or task. <see cref="ToolName"/> holds the name of whichever it was.</summary>
    public string Kind { get; set; } = "tool";
    public Guid UserId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string? Parameters { get; set; } // JSON serialized parameters
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ExecutionTimeMs { get; set; }
    public DateTime Timestamp { get; set; }

    // Navigation properties
    public virtual McpToken? McpToken { get; set; }
    public virtual OAuthGrant? Grant { get; set; }
    public virtual User User { get; set; } = null!;
}
