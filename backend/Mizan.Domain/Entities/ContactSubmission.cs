namespace Mizan.Domain.Entities;

public enum ContactSubmissionStatus
{
    New = 0,
    Handled = 1,
}

/// <summary>
/// A message sent through the public contact form. Anyone may send one; a
/// signed-in sender's account is recorded from the session, never from the
/// form. The address is kept only as a hash, enough to limit how often one
/// address may write.
/// </summary>
public class ContactSubmission
{
    public const int NameMax = 120;
    public const int EmailMax = 320;
    public const int SubjectMax = 160;
    public const int MessageMax = 5000;

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public ContactSubmissionStatus Status { get; set; } = ContactSubmissionStatus.New;
    public DateTime? HandledAt { get; set; }
    public Guid? HandledByUserId { get; set; }

    /// <summary>The reply an administrator sent, in the email log.</summary>
    public Guid? ReplyEmailMessageId { get; set; }

    public string IpHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
