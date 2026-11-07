namespace Mizan.Domain.Entities;

/// <summary>
/// One pass through the authorize endpoint. It starts pending while the user
/// reads the consent screen, becomes approved with a single-use code, and is
/// consumed when the client exchanges that code. The browser carries a secret
/// that is stored only as a hash.
/// </summary>
public class OAuthAuthorizationRequest
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Denied = "denied";
    public const string Consumed = "consumed";

    public Guid Id { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string RedirectUri { get; set; } = string.Empty;
    public List<string> RequestedScopes { get; set; } = new();
    public string? State { get; set; }
    public string CodeChallenge { get; set; } = string.Empty;

    /// <summary>Which audience the resulting tokens are for: mcp or api.</summary>
    public string Audience { get; set; } = OAuthToken.AudienceMcp;

    public string Status { get; set; } = Pending;
    public Guid? UserId { get; set; }
    public Guid? GrantId { get; set; }
    public string? CodeHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public virtual OAuthClient Client { get; set; } = null!;
}
