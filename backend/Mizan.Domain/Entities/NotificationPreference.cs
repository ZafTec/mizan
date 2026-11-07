namespace Mizan.Domain.Entities;

/// <summary>
/// A person's channels for one notification kind. A row exists only once the person changed that
/// kind; until then the kind's defaults apply (<c>NotificationKinds</c>). Telegram also needs the
/// account-wide <see cref="User.NotifyByTelegram"/> switch and a linked chat.
/// </summary>
public class NotificationPreference
{
    public Guid UserId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public bool InApp { get; set; }
    public bool Telegram { get; set; }
    public bool Push { get; set; }
    public bool Email { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
