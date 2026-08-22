namespace Mizan.Domain.Entities;

/// <summary>
/// One signed-in browser. The cookie carries a random 256-bit token; only its
/// SHA-256 hash is stored, so a database leak does not hand out live sessions.
/// </summary>
public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public virtual User? User { get; set; }
}
