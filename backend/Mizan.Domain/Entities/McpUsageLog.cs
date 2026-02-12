namespace Mizan.Domain.Entities;

public class McpUsageLog
{
    public Guid Id { get; set; }
    public Guid McpTokenId { get; set; }
    public Guid UserId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string? Parameters { get; set; } // JSON serialized parameters
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ExecutionTimeMs { get; set; }
    public DateTime Timestamp { get; set; }

    // Navigation properties
    public virtual McpToken McpToken { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
