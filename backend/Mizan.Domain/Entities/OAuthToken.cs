namespace Mizan.Domain.Entities;

/// <summary>
/// An opaque access or refresh token, stored as a hash. Refresh tokens rotate:
/// using one marks it used and issues the next in the same family. Presenting a
/// used refresh token again means it leaked, so the whole family is revoked.
/// </summary>
public class OAuthToken
{
    public const string KindAccess = "access";
    public const string KindRefresh = "refresh";
    public const string AudienceMcp = "mcp";
    public const string AudienceApi = "api";

    public Guid Id { get; set; }
    public Guid GrantId { get; set; }
    public Guid FamilyId { get; set; }
    public string Kind { get; set; } = KindAccess;
    public string Audience { get; set; } = AudienceMcp;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public virtual OAuthGrant Grant { get; set; } = null!;
}
