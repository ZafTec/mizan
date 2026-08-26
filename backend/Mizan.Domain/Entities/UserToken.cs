namespace Mizan.Domain.Entities;

public enum UserTokenPurpose
{
    EmailVerification = 0,
    PasswordReset = 1,

    /// <summary>
    /// The code behind a t.me deep link. Not mailed - it is shown on screen and
    /// lives minutes, because the user is holding both devices.
    /// </summary>
    TelegramLink = 2,
}

/// <summary>
/// A single-use token mailed to the user. Stored as a hash for the same reason
/// sessions are, and consumed rather than deleted so a replayed link can say
/// "already used" instead of "invalid".
/// </summary>
public class UserToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserTokenPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public virtual User? User { get; set; }
}
