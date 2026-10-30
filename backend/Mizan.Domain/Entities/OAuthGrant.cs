namespace Mizan.Domain.Entities;

/// <summary>
/// What one user allowed one client to do. Tokens point at the grant and read
/// its scopes at request time, so reducing a grant takes effect at once and
/// revoking it ends every token.
/// </summary>
public class OAuthGrant
{
    public const string HouseholdsNone = "none";
    public const string HouseholdsSelected = "selected";
    public const string HouseholdsAll = "all";

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ClientId { get; set; }

    public List<string> Scopes { get; set; } = new();

    /// <summary>none: personal data only. selected: only <see cref="HouseholdIds"/>. all: every household the user belongs to.</summary>
    public string HouseholdMode { get; set; } = HouseholdsNone;

    public List<Guid> HouseholdIds { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null;

    public virtual User User { get; set; } = null!;
    public virtual OAuthClient Client { get; set; } = null!;
}
