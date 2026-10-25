namespace Mizan.Domain.Entities;

/// <summary>
/// An email an administrator sent to many people at once. The row exists so a
/// retried request finds the first send rather than starting a second one,
/// and so the hourly limit on all-user announcements has something to count.
/// Each recipient's message is an ordinary <see cref="EmailMessage"/>.
/// </summary>
public class Announcement
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;

    /// <summary>As the administrator chose it: all, pro, free, household:{id} or user:{id}.</summary>
    public string Audience { get; set; } = string.Empty;

    public int RecipientCount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
