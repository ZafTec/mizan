using Mizan.Application.Ai.Tools;
using Mizan.Contracts.Ai;

namespace Mizan.Application.Interfaces;

/// <summary>
/// What one tool call did, and the checked cards it produced. <c>ConsentRequired</c> is set when
/// the call was refused because the person has not allowed that axis.
/// </summary>
public record AiToolRun(
    AiToolInvocation Invocation, IReadOnlyList<AiCardDto> Cards, AiConsentRequirement? ConsentRequired = null);

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
    Task<AiToolInvocation> RunAsync(
        AiToolCall call, AiToolContext context, CancellationToken cancellationToken = default);

    /// <summary>The same call, with the cards built from its result after each passed <see cref="Ai.AiCardValidator"/>.</summary>
    Task<AiToolRun> RunWithCardsAsync(
        AiToolCall call, AiToolContext context, CancellationToken cancellationToken = default);
}
