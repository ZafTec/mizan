using Mizan.Application.Ai.Tools;

namespace Mizan.Application.Interfaces;

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
}
