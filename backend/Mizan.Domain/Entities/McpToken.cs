namespace Mizan.Domain.Entities;

public class McpToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty; // SHA256 hash
    public string Name { get; set; } = string.Empty; // e.g., "Claude Desktop - Home PC"
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; } // Optional expiration
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public virtual User User { get; set; } = null!;
}
