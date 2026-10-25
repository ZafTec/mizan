namespace Mizan.Domain.Entities;

public enum EmailMessageStatus
{
    Queued = 0,
    Sent = 1,
    Failed = 2,
}

/// <summary>
/// One email the system sent, or tried to. The row is written when the email is
/// queued, in the same unit of work as its outbox job, and the email handler
/// moves it to Sent or Failed. It is the one log an administrator reads to see
/// whether a message left.
///
/// The body is never stored: a verification or reset email carries a token,
/// and the outbox payload already holds the body for exactly as long as the
/// job needs it. Recipient, subject and kind are enough to find a message.
/// </summary>
public class EmailMessage
{
    public Guid Id { get; set; }
    public Guid? ToUserId { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    /// <summary>What sent it, as a dotted name. See <c>EmailKinds</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public EmailMessageStatus Status { get; set; } = EmailMessageStatus.Queued;
    public int Attempts { get; set; }

    /// <summary>The last delivery error, with addresses already redacted.</summary>
    public string? LastError { get; set; }

    /// <summary>The job that carries the body. Not a foreign key: an administrator may delete a finished job.</summary>
    public Guid? OutboxJobId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }

    /// <summary>The administrator who wrote it, for announcements and replies. Null for system email.</summary>
    public Guid? CreatedByUserId { get; set; }
}

/// <summary>The kinds of email the system sends. The column is open text; these are the ones it writes.</summary>
public static class EmailKinds
{
    public const string AuthVerify = "auth.verify";
    public const string AuthReset = "auth.reset";
    public const string AuthSignIn = "auth.signin";
    public const string Announcement = "admin.announcement";
    public const string ContactReceived = "contact.received";
    public const string ContactReply = "contact.reply";
    public const string SettingsTest = "settings.test";
}
