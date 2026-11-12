using System.Text.Json;

namespace Mizan.Contracts.Tools;

/// <summary>Where a task tool is offered: the MCP connector (and other outside agents) or the in-app assistant.</summary>
public static class ToolSurfaces
{
    public const string Mcp = "mcp";
    public const string Assistant = "assistant";
}

/// <summary>Whether a task tool looks, changes, or removes. MCP hosts read it as the tool's annotations.</summary>
public static class ToolKinds
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Destructive = "destructive";
}

/// <summary>
/// The task tools one caller may use on one surface (<c>GET /api/tools</c>). Each schema's mode
/// enum is narrowed to the modes the caller's role and connection allow.
/// </summary>
public sealed record ToolManifestDto(string Surface, IReadOnlyList<ToolDescriptorDto> Tools);

public sealed record ToolDescriptorDto(
    string Name,
    string Title,
    string Description,
    string Kind,
    JsonElement InputSchema,
    /// <summary>The MCP app page this tool renders in a host that shows apps, or null.</summary>
    string? App,
    /// <summary>What a host may assume of the tool, true for its worst mode (docs/MCP.md#tool-hints-and-replies).</summary>
    ToolHintsDto? Hints = null);

/// <summary>
/// A task tool's MCP annotations. Each holds for every mode the tool has: a tool with a mode that replaces or removes
/// something is destructive, and one that may fetch an address the caller gave is open-world.
/// </summary>
public sealed record ToolHintsDto(bool ReadOnly, bool Destructive, bool Idempotent, bool OpenWorld);

/// <summary>A call to one task tool (<c>POST /api/tools/{name}/call</c>).</summary>
public sealed record ToolCallRequest(string? Surface, JsonElement? Arguments);

/// <summary>
/// What a tool call came to. <c>Data</c> is the command's result as JSON; <c>Text</c> is the line a
/// model reads. A refused or failed call has <c>IsError</c> and an <c>ErrorCode</c>.
/// </summary>
public sealed record ToolCallResultDto(
    string Tool,
    string? Mode,
    bool IsError,
    string Text,
    JsonElement? Data,
    string? ErrorCode = null,
    /// <summary>The scope a refused call needed, so the client can say where to grant it.</summary>
    string? Needs = null,
    /// <summary>Set when the call previewed a high-impact action instead of doing it.</summary>
    ToolConfirmationDto? Confirmation = null);

/// <summary>
/// A previewed action waiting for its confirmation. Send <c>Token</c> back with the tool's
/// <c>confirm</c> mode (or <c>confirm_action</c>) before <c>ExpiresAt</c>; it works once.
/// </summary>
public sealed record ToolConfirmationDto(string Token, DateTime ExpiresAt, JsonElement Preview);

/// <summary>The fixed lists the task tools take values from, served as the resource <c>mizan://reference/price-regions</c>.</summary>
public sealed record ToolReferenceDto(
    IReadOnlyList<ToolReferencePriceRegion> PriceRegions,
    IReadOnlyList<string> GoalTypes,
    IReadOnlyList<string> MealSlots,
    IReadOnlyList<string> MacroModes,
    IReadOnlyList<string> ReminderKinds);

public sealed record ToolReferencePriceRegion(string Code, string Name, string Currency);
