using System.Text.Json.Nodes;
using MediatR;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Domain.Ai;

namespace Mizan.Application.Tools;

/// <summary>Whether a tool looks, changes, or removes. A host reads it as the tool's annotations.</summary>
public enum ToolKind
{
    Read,
    Write,
    Destructive,
}

/// <summary>Where a mode is offered. A mode outside the caller's surface is not listed and is refused.</summary>
[Flags]
public enum ToolSurface
{
    Mcp = 1,
    Assistant = 2,
    Both = Mcp | Assistant,
}

/// <summary>The least role a mode needs. A trainer is a coach; an administrator is not a coach by that alone.</summary>
public enum ToolRole
{
    User,
    Trainer,
    Admin,
}

/// <summary>
/// Who is calling and from where, as the session and the connection say. A builder takes
/// ownership from here, never from the arguments, so a caller cannot act for someone else by
/// writing an id (docs/AI.md#consent). <paramref name="Impersonating"/> is set while an administrator
/// views Mizan as someone else; no admin mode is offered then. <paramref name="Gateway"/> is set when a hosted
/// assistant acts for the person through the gateway: their assistant consent applies to every call
/// (<see cref="AssistantConsent"/>) and no admin mode is offered (docs/MCP.md).
/// </summary>
public sealed record ToolCallContext(Guid UserId, ToolSurface Surface, string Role, GrantContext? Grant, bool Impersonating = false, bool Gateway = false);

/// <summary>
/// A previewed high-impact action. The preview request reads what the action would remove,
/// and <see cref="Facts"/> turns its result into what the person is shown and what must still
/// hold when they confirm (docs/MCP.md#confirming-a-high-impact-action).
/// </summary>
public sealed record ToolConfirmStep(
    Type Request,
    Func<ToolArgs, ToolCallContext, IBaseRequest> BuildPreview,
    Func<object?, JsonNode> Facts);

/// <summary>
/// One mode of a task tool: the area it belongs to, who may use it, where it is offered, and the
/// existing MediatR request it builds. A mode adds no rule of its own; the command it builds
/// holds them, the same for the web, MCP, Telegram and the assistant.
/// </summary>
public sealed class ToolMode
{
    /// <summary>A mode open to every connection, whatever it was granted (the profile, the plan, AI usage).</summary>
    public const string AnyConnection = "*";

    /// <summary>A mode open to a connection that may change at least one area.</summary>
    public const string AnyWrite = "*:write";

    /// <summary>The value of the tool's mode argument that selects this mode.</summary>
    public required string Name { get; init; }

    /// <summary>One line, shown to the model next to the mode's value.</summary>
    public required string Summary { get; init; }

    /// <summary>The area scope the mode needs, such as <c>nutrition:read</c>, or <see cref="AnyConnection"/> or <see cref="AnyWrite"/>.</summary>
    public required string Area { get; init; }

    public ToolRole MinimumRole { get; init; } = ToolRole.User;

    public ToolSurface Surfaces { get; init; } = ToolSurface.Mcp;

    /// <summary>The MediatR request types <see cref="Build"/> may return. A test holds each one to a real handler.</summary>
    public required IReadOnlyList<Type> Requests { get; init; }

    public required Func<ToolArgs, ToolCallContext, IBaseRequest> Build { get; init; }

    /// <summary>What the caller receives in place of the command's result, when it differs.</summary>
    public Func<object?, object?>? Shape { get; init; }

    /// <summary>The result in words, for the assistant's model. Without it the model reads the JSON.</summary>
    public Func<object?, string>? Describe { get; init; }

    /// <summary>Typed cards built by code from the result, for the assistant's surfaces.</summary>
    public Func<object?, IReadOnlyList<AiCardDraft>>? Cards { get; init; }

    /// <summary>The legacy MCP tools this mode replaces. A grant made before the areas reaches the mode through their scopes.</summary>
    public IReadOnlyList<string> Replaces { get; init; } = [];

    /// <summary>The axis the assistant's consent is checked on. Taken from <see cref="Area"/> when not set.</summary>
    public DataAxis? Axis { get; init; }

    /// <summary>Set for a high-impact action: the call previews it and returns a token instead of doing it.</summary>
    public ToolConfirmStep? Confirm { get; init; }

    /// <summary>The message when the command answers with nothing, such as an id that names no record. Null passes the empty result on.</summary>
    public string? NotFound { get; init; }

    /// <summary>
    /// Narrows the result to the part the arguments ask for, before it is shaped and described, so a reply stays well
    /// under the 32 KB a host takes (docs/MCP.md#reply-size). A month's meal plan is answered four days at a time.
    /// </summary>
    public Func<object?, ToolArgs, object?>? Window { get; init; }

    public bool Writes => Area.EndsWith(":write", StringComparison.Ordinal);
}

/// <summary>
/// A task tool: one name, one description and one input schema, used by every surface. Its
/// modes are chosen by one argument (<see cref="ModeArgument"/>); a tool with a single mode has none.
/// </summary>
public sealed class TaskTool
{
    public required string Name { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required ToolKind Kind { get; init; }

    /// <summary>The argument that picks the mode, such as <c>view</c>, <c>kind</c> or <c>action</c>. Null for a single-mode tool.</summary>
    public string? ModeArgument { get; init; }

    /// <summary>The mode used when the argument is left out.</summary>
    public string? DefaultMode { get; init; }

    /// <summary>The tool's other arguments, as JSON Schema properties. The mode argument is added from the modes.</summary>
    public required JsonObject Properties { get; init; }

    public IReadOnlyList<string> Required { get; init; } = [];

    public required IReadOnlyList<ToolMode> Modes { get; init; }

    /// <summary>The MCP app page (<c>ui://mizan/&lt;name&gt;.html</c>) this tool renders, if any.</summary>
    public string? App { get; init; }

    /// <summary>
    /// Every mode only adds something or spends an allowance, and none replaces or removes what the person entered.
    /// A write tool without it is destructive in its annotations, because hints must hold for its worst mode.
    /// </summary>
    public bool Additive { get; init; }

    /// <summary>Calling it again with the same arguments changes nothing more. Read tools always are.</summary>
    public bool Idempotent { get; init; }

    /// <summary>A mode may fetch an address the caller gave (an image URL), so the tool reaches beyond Mizan.</summary>
    public bool OpenWorld { get; init; }

    /// <summary>
    /// Describes the mode argument in place of one line per mode, for a tool with many modes that
    /// another tool describes (run_action, whose actions find_action lists).
    /// </summary>
    public string? ModeHelp { get; init; }

    /// <summary>
    /// The tool that carries out this tool's previews. Null gives the tool its own <c>confirm</c> mode;
    /// run_action names confirm_action, which a host asks the person before calling.
    /// </summary>
    public string? ConfirmTool { get; init; }

    /// <summary>The mode the arguments select, or null when they name none this tool has.</summary>
    public ToolMode? ModeFor(ToolArgs args)
    {
        if (ModeArgument is null) return Modes.Count == 1 ? Modes[0] : null;
        var name = args.OptionalString(ModeArgument) ?? DefaultMode;
        return name is null ? null : Modes.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));
    }
}
