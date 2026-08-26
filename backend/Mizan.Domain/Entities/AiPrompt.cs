namespace Mizan.Domain.Entities;

public enum AiPromptStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

/// <summary>
/// One programmable surface. The key is stable and referenced from code;
/// everything about how it speaks lives in its versions.
/// </summary>
public class AiPrompt
{
    public Guid Id { get; set; }

    /// <summary>"chat.system", "food.analysis". Code asks for a key, never a version.</summary>
    public string Key { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AiPromptVersion> Versions { get; set; } = new List<AiPromptVersion>();
}

/// <summary>
/// An immutable revision. Published versions are never edited - a change is a
/// new version - so an answer can always be traced to the exact text that
/// produced it, and a rollback is a pointer move rather than a rewrite
/// (docs/REFOCUS.md §12).
/// </summary>
public class AiPromptVersion
{
    public Guid Id { get; set; }
    public Guid PromptId { get; set; }
    public int Version { get; set; }

    /// <summary>The editable half. Tone, framing, how it introduces itself.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Structured soft policy, as JSON. Knobs, not rules.</summary>
    public string SoftPolicy { get; set; } = "{}";

    public AiPromptStatus Status { get; set; } = AiPromptStatus.Draft;
    public Guid? AuthorId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public virtual AiPrompt? Prompt { get; set; }
}
