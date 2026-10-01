namespace Mizan.Domain.Entities;

/// <summary>A phone that can be sent a push notification for this user.</summary>
public class DeviceToken
{
    public const string Android = "android";

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Platform { get; set; } = Android;

    /// <summary>The push provider's address for the device. One token belongs to one user at a time.</summary>
    public string Token { get; set; } = string.Empty;

    public string? DeviceName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    public virtual User User { get; set; } = null!;
}
