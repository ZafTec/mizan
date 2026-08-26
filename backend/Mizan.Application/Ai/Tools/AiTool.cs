using System.Text.Json;
using MediatR;

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
public record AiToolDefinition(
    string Name,
    string Description,
    string ParametersSchema,
    Func<JsonElement, AiToolContext, IBaseRequest> Build,
    Func<object?, string> Describe);

/// <summary>What a tool call did, echoed back so the UI can say "I set X".</summary>
public record AiToolInvocation(string Tool, string Summary, bool Succeeded, string? Error = null);
