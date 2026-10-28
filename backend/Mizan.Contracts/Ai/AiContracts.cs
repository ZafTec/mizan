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
    /// <summary>
    /// The user's message as stored, with the address of any photo. The web
    /// client shows a local preview until this arrives, then swaps to it.
    /// </summary>
    AiChatMessageDto Message,
    AiChatMessageDto Reply,
    /// <summary>
    /// What the turn actually did. Echoed for the same reason onboarding
    /// echoes it: an assistant that writes to someone's log without saying so
    /// is indistinguishable from one making things up.
    /// </summary>
    IReadOnlyList<AiToolInvocation> Performed,
    /// <summary>
    /// Typed cards from what the turn did: a generated plan, a day, a cost table, a shopping
    /// list. Built by code from command results and checked against their records; see
    /// <see cref="AiCardTypes"/>.
    /// </summary>
    IReadOnlyList<AiCardDto> Cards,
    /// <summary>
    /// The permissions the turn's tool calls were refused for, one per axis and access, so a
    /// surface can offer to grant exactly that. Empty when nothing was refused. The reply still
    /// carries the model's own explanation.
    /// </summary>
    IReadOnlyList<AiConsentRequirement> ConsentRequired);

/// <summary>
/// A tool call the person's AI consent did not allow. <c>ErrorCode</c> is always
/// <see cref="AiErrorCodes.ConsentRequired"/>; <c>Axis</c> is the switch to turn on
/// (<c>nutrition</c>, <c>training</c> or <c>body</c>; meal plans and shopping lists report
/// <c>nutrition</c>, whose switches they share), and <c>Access</c> is <c>read</c> or <c>write</c>.
/// </summary>
public record AiConsentRequirement(string ErrorCode, string Axis, string Access);

public static class AiErrorCodes
{
    public const string ConsentRequired = "ai_consent_required";
}

/// <summary>One row of GET /api/Ai/threads, newest first.</summary>
public record AiChatThreadDto(Guid Id, string Title, DateTime UpdatedAt);
