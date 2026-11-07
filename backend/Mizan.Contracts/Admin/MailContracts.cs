namespace Mizan.Contracts.Admin;

/// <summary>One email in the log at /api/admin/mail. The body is never stored, so it is never shown.</summary>
public record EmailMessageDto
{
    public Guid Id { get; init; }
    public Guid? ToUserId { get; init; }
    public string ToAddress { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;

    /// <summary>Queued, Sent or Failed.</summary>
    public string Status { get; init; } = string.Empty;

    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public Guid? OutboxJobId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SentAt { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public string? CreatedByName { get; init; }

    /// <summary>True when POST /{id}/retry would queue it again: not sent, not already queued, and its job still holds the body.</summary>
    public bool CanRetry { get; init; }
}

/// <summary>Counts for the header of the mail page, and the kinds present, for its filter.</summary>
public record MailStatsDto(int Queued, int Sent, int Failed, IReadOnlyList<string> Kinds);

/// <summary>
/// Body of POST /api/admin/mail/announcements. Audience is all, pro, free,
/// household:{id} or user:{id}. Send the same <see cref="AnnouncementId"/> on a
/// retry and nobody gets the message twice.
/// </summary>
public record SendAnnouncementRequest(string Subject, string Body, string Audience, Guid? AnnouncementId = null);

/// <summary>What an announcement queued: one message per recipient.</summary>
public record AnnouncementResultDto(Guid AnnouncementId, string Audience, int Queued);

/// <summary>GET /api/admin/mail/announcements/audience: who a choice of audience reaches, for the confirmation step.</summary>
public record AnnouncementAudienceDto(string Audience, string Label, int RecipientCount);
