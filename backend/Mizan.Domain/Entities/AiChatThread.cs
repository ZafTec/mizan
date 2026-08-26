namespace Mizan.Domain.Entities;

public enum AiChatRole
{
    User = 0,
    Assistant = 1,
}

/// <summary>
/// A conversation with the assistant.
///
/// This used to be a single jsonb blob called ThreadData holding "thread
/// state", which nothing ever wrote and nothing could have queried. Messages
/// are rows now: a turn traces to the prompt version that produced it, and a
/// bad answer is findable rather than buried in a serialized lump
/// (docs/REFOCUS.md §12).
/// </summary>
public class AiChatThread
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Taken from the opening question. A conversation nobody can identify is a conversation nobody returns to.</summary>
    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual ICollection<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
}

public class AiChatMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public AiChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>Which published prompt answered. Null on a user turn, and null when the built-in default answered.</summary>
    public Guid? PromptVersionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AiChatThread? Thread { get; set; }
}
