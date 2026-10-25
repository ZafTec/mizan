namespace Mizan.Contracts.Admin;

/// <summary>
/// Body of the public POST /api/Contact. <see cref="Website"/> is the form's
/// hidden field: people leave it empty and a form-filling bot does not, and a
/// filled one is accepted and dropped.
/// </summary>
public record ContactRequest(string Name, string Email, string Subject, string Message, string? Website = null);

/// <summary>The same answer whether the message was kept or dropped, so a bot learns nothing.</summary>
public record ContactAcceptedDto(bool Accepted);

public record ContactSubmissionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Guid? UserId { get; init; }

    /// <summary>New or Handled.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
    public DateTime? HandledAt { get; init; }
    public Guid? HandledByUserId { get; init; }
    public string? HandledByName { get; init; }
    public Guid? ReplyEmailMessageId { get; init; }

    /// <summary>Queued, Sent or Failed: where the reply is, when there is one.</summary>
    public string? ReplyStatus { get; init; }
}

/// <summary>Body of POST /api/admin/contact/{id}/reply. Plain text.</summary>
public record ContactReplyRequest(string Body);
