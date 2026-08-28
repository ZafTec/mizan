using System.Text.Json;
using MediatR;
using Mizan.Domain.Ai;

namespace Mizan.Application.Ai.Tools;

/// <summary>
/// Everything the caller may supply. Anything identifying <em>who</em> the
/// action is for is deliberately absent - see <see cref="AiToolCatalogue"/>.
/// </summary>
public record AiToolContext(Guid UserId);

/// <summary>
/// One allowlisted action: a name, a schema the model is given, and a factory
/// that turns validated JSON into an existing MediatR request.
///
/// The factory is the whole security boundary. It receives the parsed
/// arguments and the caller's identity separately, so a model cannot supply a
/// user id by writing one into its arguments (docs/REFOCUS.md §10).
/// </summary>
/// <summary>
/// Whether a tool looks or acts. The two are consented to separately, so the
/// runner has to know which question to ask.
/// </summary>
public enum AiToolAccess
{
    Read = 0,
    Write = 1,
}

public record AiToolDefinition(
    string Name,
    string Description,
    string ParametersSchema,
    Func<JsonElement, AiToolContext, IBaseRequest> Build,
    Func<object?, string> Describe,
    /// <summary>
    /// Which axis of the log this touches. The runner refuses the call unless
    /// the user has granted that axis, so a tool that forgets to declare one
    /// honestly cannot be added: there is no default.
    /// </summary>
    DataAxis Axis,
    AiToolAccess Access);

/// <summary>What a tool call did, echoed back so the UI can say "I set X".</summary>
public record AiToolInvocation(string Tool, string Summary, bool Succeeded, string? Error = null);
