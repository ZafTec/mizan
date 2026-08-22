namespace Mizan.Domain.Entities;

/// <summary>
/// Links a Google or GitHub account to a Mizan user. The provider's own user id
/// is the key, never the email - emails change hands, provider ids do not.
/// </summary>
public class ExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
