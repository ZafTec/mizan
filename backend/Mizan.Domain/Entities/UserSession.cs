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

    /// <summary>
    /// Set when an administrator opened this session to see the site as the user. Such a session is short, cannot
    /// change credentials or billing, and every audited action in it names the administrator.
    /// </summary>
    public Guid? ImpersonatorId { get; set; }

    public virtual User? User { get; set; }
}
