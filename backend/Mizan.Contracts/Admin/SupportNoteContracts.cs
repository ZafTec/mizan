namespace Mizan.Contracts.Admin;

/// <summary>An administrator's note about a user or a household. Only administrators can read one.</summary>
public record SupportNoteDto
{
    public Guid Id { get; init; }

    /// <summary>User or Household.</summary>
    public string SubjectType { get; init; } = string.Empty;

    public Guid SubjectId { get; init; }
    public string Body { get; init; } = string.Empty;
    public Guid AuthorUserId { get; init; }
    public string? AuthorName { get; init; }
    public string? AuthorEmail { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Body of POST /api/admin/support-notes.</summary>
public record CreateSupportNoteRequest(string SubjectType, Guid SubjectId, string Body);

/// <summary>Body of PUT /api/admin/support-notes/{id}.</summary>
public record UpdateSupportNoteRequest(string Body);
