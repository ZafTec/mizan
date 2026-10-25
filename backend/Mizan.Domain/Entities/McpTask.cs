namespace Mizan.Domain.Entities;

/// <summary>
/// A long-running MCP call (an assistant reply, a photo analysis) that the
/// client polls instead of waiting for. The state lives here, not in the MCP
/// process, so any instance can answer a poll.
/// </summary>
public class McpTask
{
    public const string Working = "working";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    /// <summary>Random and unguessable. It is the only handle a client holds.</summary>
    public string Id { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public Guid? GrantId { get; set; }
    public string Status { get; set; } = Working;
    public string? StatusMessage { get; set; }

    /// <summary>The tool result, as JSON, once the task has completed.</summary>
    public string? ResultJson { get; set; }

    /// <summary>The JSON-RPC error, as JSON, if the task failed.</summary>
    public string? ErrorJson { get; set; }

    public int TtlSeconds { get; set; } = 3600;
    public int PollIntervalMs { get; set; } = 2000;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
