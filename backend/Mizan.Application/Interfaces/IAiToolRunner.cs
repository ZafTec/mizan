using Mizan.Application.Ai.Tools;
using Mizan.Contracts.Ai;

namespace Mizan.Application.Interfaces;

/// <summary>
/// What one tool call did, and the checked cards it produced. <c>ConsentRequired</c> is set when
/// the call was refused because the person has not allowed that axis.
/// </summary>
public record AiToolRun(
    AiToolInvocation Invocation, IReadOnlyList<AiCardDto> Cards, AiConsentRequirement? ConsentRequired = null)
{
    /// <summary>What the model reads back, when it is more than the line the person sees.</summary>
    public string? ModelText { get; init; }
}

/// <summary>
/// Runs one allowlisted tool call as the calling user.
///
/// Never throws for a bad call: an unknown tool, invalid arguments or a
/// rejected command all come back as a failed <see cref="AiToolInvocation"/>,
/// because the model needs to read the reason and try again rather than take
/// the whole turn down with it.
/// </summary>
public interface IAiToolRunner
{
    /// <summary>The chat assistant's tools for this user: the task tools' assistant view (<see cref="AiToolCatalogue.Assistant"/>).</summary>
    IReadOnlyList<AiToolSpec> AssistantTools(Guid userId);

    /// <summary>One chat assistant call: a task tool, held to the person's consent on the mode's axis, with its cards after each passed <see cref="Ai.AiCardValidator"/>.</summary>
    Task<AiToolRun> RunAssistantAsync(
        AiToolCall call, AiToolContext context, CancellationToken cancellationToken = default);

    /// <summary>One onboarding call, from <see cref="AiToolCatalogue.Onboarding"/>.</summary>
    Task<AiToolInvocation> RunOnboardingAsync(
        AiToolCall call, AiToolContext context, CancellationToken cancellationToken = default);
}
