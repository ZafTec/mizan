namespace Mizan.Domain.Entities;

public enum SupportNoteSubject
{
    User = 0,
    Household = 1,
}

/// <summary>
/// An administrator's note about a user or a household, for the next
/// administrator who looks. Only administrators read or write them; the
/// subject never sees them.
/// </summary>
public class SupportNote
{
    public const int BodyMax = 4000;

    public Guid Id { get; set; }
    public SupportNoteSubject SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>Not a foreign key, so a note outlives the administrator who wrote it.</summary>
    public Guid AuthorUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
