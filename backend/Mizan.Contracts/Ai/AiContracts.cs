namespace Mizan.Contracts.Ai;

/// <summary>What a tool call did, echoed back so the UI can say "I set X".</summary>
public record AiToolInvocation(string Tool, string Summary, bool Succeeded, string? Error = null);

/// <summary>One message of a thread, as GET /api/Ai/threads/{id} and POST /api/Ai/chat return it.</summary>
public record AiChatMessageDto(
    Guid Id, bool FromUser, string Content, DateTime CreatedAt, string? ImageUrl = null);

/// <summary>Body of the POST /api/Ai/chat response.</summary>
public record AiChatTurnDto(
    Guid ThreadId,
    string Title,
    AiChatMessageDto Reply,
    /// <summary>
    /// What the turn actually did. Echoed for the same reason onboarding
    /// echoes it: an assistant that writes to someone's log without saying so
    /// is indistinguishable from one making things up.
    /// </summary>
    IReadOnlyList<AiToolInvocation> Performed);

/// <summary>One row of GET /api/Ai/threads, newest first.</summary>
public record AiChatThreadDto(Guid Id, string Title, DateTime UpdatedAt);
