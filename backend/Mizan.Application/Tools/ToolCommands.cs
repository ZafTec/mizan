using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Tools;
using Mizan.Domain.Constants;

namespace Mizan.Application.Tools;

/// <summary>The task tools the caller may use on one surface, each schema narrowed to the modes they may use.</summary>
public sealed record GetToolManifestQuery(ToolSurface Surface) : IRequest<ToolManifestDto>;

public sealed class GetToolManifestQueryHandler : IRequestHandler<GetToolManifestQuery, ToolManifestDto>
{
    private readonly ICurrentUserService _currentUser;

    public GetToolManifestQueryHandler(ICurrentUserService currentUser) => _currentUser = currentUser;

    public Task<ToolManifestDto> Handle(GetToolManifestQuery request, CancellationToken cancellationToken)
    {
        var context = ToolCalls.ContextOf(_currentUser, request.Surface);
        var tools = TaskToolCatalogue.All
            .Select(tool => ToolFilter.Describe(tool, context))
            .OfType<ToolDescriptorDto>()
            .ToList();
        return Task.FromResult(new ToolManifestDto(ToolFilter.SurfaceName(request.Surface), tools));
    }
}

/// <summary>
/// One task tool call. The arguments may carry a confirmation token, so the audit row records the
/// tool, the mode, the outcome and the confirmed action's id, never the arguments themselves.
/// </summary>
public sealed class CallTaskToolCommand : IRequest<ToolRunResult>, IRedactedAudit
{
    public CallTaskToolCommand(string tool, JsonElement arguments, ToolSurface surface)
    {
        Tool = tool;
        Arguments = arguments;
        Surface = surface;
    }

    public string Tool { get; }
    public JsonElement Arguments { get; }
    public ToolSurface Surface { get; }

    /// <summary>Filled in by the handler so the audit row says what the call came to.</summary>
    internal ToolRunResult? Outcome { get; set; }

    public object AuditDetails => new
    {
        Tool,
        Mode = Outcome?.Mode,
        Surface = ToolFilter.SurfaceName(Surface),
        Outcome = Outcome is null ? null : Outcome.IsError ? Outcome.ErrorCode : Outcome.Confirmation is not null ? "previewed" : "done",
        ConfirmedAction = Outcome?.ConfirmedAction,
    };
}

public sealed class CallTaskToolCommandHandler : IRequestHandler<CallTaskToolCommand, ToolRunResult>
{
    private readonly ITaskToolRunner _runner;
    private readonly ICurrentUserService _currentUser;

    public CallTaskToolCommandHandler(ITaskToolRunner runner, ICurrentUserService currentUser)
    {
        _runner = runner;
        _currentUser = currentUser;
    }

    public async Task<ToolRunResult> Handle(CallTaskToolCommand request, CancellationToken cancellationToken)
    {
        var context = ToolCalls.ContextOf(_currentUser, request.Surface);
        var result = await _runner.RunAsync(request.Tool, new ToolArgs(request.Arguments), context, cancellationToken);
        request.Outcome = result;
        return result;
    }
}

/// <summary>
/// Carries out a previewed high-impact action. The token is the only thing the caller holds; the
/// request is built again from what was stored at the preview (docs/MCP.md#confirming-a-high-impact-action).
/// </summary>
public sealed class ConfirmToolActionCommand : IRequest<ToolRunResult>, IRedactedAudit
{
    public ConfirmToolActionCommand(string token, ToolSurface surface)
    {
        Token = token;
        Surface = surface;
    }

    public string Token { get; }
    public ToolSurface Surface { get; }

    internal Guid? ConfirmedAction { get; set; }

    public object AuditDetails => new { ConfirmedAction, Surface = ToolFilter.SurfaceName(Surface) };
}

public sealed class ConfirmToolActionCommandHandler : IRequestHandler<ConfirmToolActionCommand, ToolRunResult>
{
    private readonly ITaskToolRunner _runner;
    private readonly ICurrentUserService _currentUser;

    public ConfirmToolActionCommandHandler(ITaskToolRunner runner, ICurrentUserService currentUser)
    {
        _runner = runner;
        _currentUser = currentUser;
    }

    public async Task<ToolRunResult> Handle(ConfirmToolActionCommand request, CancellationToken cancellationToken)
    {
        var result = await _runner.ConfirmAsync(request.Token, ToolCalls.ContextOf(_currentUser, request.Surface), cancellationToken);
        request.ConfirmedAction = result.ConfirmedAction;
        return result;
    }
}

/// <summary>The fixed lists the tools take values from, served as <c>mizan://reference/price-regions</c>.</summary>
public sealed record GetToolReferenceQuery : IRequest<ToolReferenceDto>;

public sealed class GetToolReferenceQueryHandler : IRequestHandler<GetToolReferenceQuery, ToolReferenceDto>
{
    private readonly IMizanDbContext _context;

    public GetToolReferenceQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<ToolReferenceDto> Handle(GetToolReferenceQuery request, CancellationToken cancellationToken)
    {
        var regions = await _context.PriceRegions.AsNoTracking()
            .Where(r => r.Enabled)
            .OrderBy(r => r.Name)
            .Select(r => new ToolReferencePriceRegion(r.Code, r.Name, r.Currency))
            .ToListAsync(cancellationToken);

        return new ToolReferenceDto(
            regions,
            ToolReference.GoalTypes,
            ToolReference.MealSlots,
            ToolReference.MacroModes,
            ToolReference.ReminderKinds);
    }
}

public static class ToolCalls
{
    public static ToolCallContext ContextOf(ICurrentUserService currentUser, ToolSurface surface) =>
        new(currentUser.UserId ?? throw new UnauthorizedAccessException(), surface, currentUser.Role ?? "user", currentUser.Grant);
}

/// <summary>The vocabularies of the tools' arguments, from the Domain lists the commands validate against.</summary>
public static class ToolReference
{
    public static IReadOnlyList<string> GoalTypes { get; } = Domain.Goals.GoalTypes.All;
    public static IReadOnlyList<string> MealSlots { get; } = MealTypes.All.Where(t => t != MealTypes.Meal).ToList();
    public static IReadOnlyList<string> MacroModes { get; } = Enum.GetNames<Domain.Planning.MacroMode>();
    public static IReadOnlyList<string> ReminderKinds { get; } = Domain.Notifications.ReminderKinds.All;
}
