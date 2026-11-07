using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Ai;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Tools;
using Mizan.Contracts.Ai;
using Mizan.Domain.Ai;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// The only bridge between a model and a command.
///
/// The chat assistant's calls are task tools (<see cref="TaskToolCatalogue"/>) and run through
/// <see cref="ITaskToolRunner"/> on the assistant surface, the same runner MCP uses. Onboarding's
/// own short list goes through <see cref="IMediator"/> directly. Either way the pipeline the HTTP
/// path uses applies unchanged: FluentValidation rejects the same arguments, and AuditBehavior
/// records the write with the same attribution. A tool call is an ordinary command that happened
/// to be asked for by a model (docs/AI.md).
/// </summary>
public class AiToolRunner : IAiToolRunner
{
    private readonly IMediator _mediator;
    private readonly IMizanDbContext _context;
    private readonly ITaskToolRunner _tasks;
    private readonly ILogger<AiToolRunner> _logger;
    private readonly ICurrentUserService? _currentUser;

    public AiToolRunner(
        IMediator mediator, IMizanDbContext context, ITaskToolRunner tasks, ILogger<AiToolRunner> logger,
        ICurrentUserService? currentUser = null)
    {
        _mediator = mediator;
        _context = context;
        _tasks = tasks;
        _logger = logger;
        _currentUser = currentUser;
    }

    public IReadOnlyList<AiToolSpec> AssistantTools(Guid userId) => AiToolCatalogue.Assistant(CallContext(userId));

    public async Task<AiToolRun> RunAssistantAsync(
        AiToolCall call, AiToolContext context, CancellationToken cancellationToken = default)
    {
        var tool = TaskToolCatalogue.Find(call.Name);
        if (tool is null)
        {
            // Not an error worth logging loudly: a model asking for a tool it
            // was not given is a model being told no, which is the system
            // working.
            return Failed(call.Name, $"There is no tool called '{call.Name}'.");
        }

        ToolArgs args;
        try
        {
            args = ToolArgs.Parse(call.Arguments);
        }
        catch (JsonException)
        {
            return Failed(call.Name, "The arguments were not valid JSON.");
        }

        var callContext = CallContext(context.UserId);
        var mode = tool.ModeFor(args);

        // A mode the arguments do not name, or one not offered to the assistant, is refused by
        // the task runner in its own words. Every other mode is held to consent first.
        if (mode is not null && ToolFilter.Refusal(mode, callContext with { Grant = null }) is null)
        {
            if (AiToolCatalogue.AxisOf(mode) is not { } axis)
            {
                // A mode with no consent axis is never run by the assistant, whatever its surfaces say.
                return Failed(call.Name, $"{tool.Name} ({mode.Name}) is not offered here.");
            }

            var access = mode.Writes ? AiToolAccess.Write : AiToolAccess.Read;

            // A connected app is held to the mode's area, as MCP holds it.
            if (callContext.Grant is { } grant && !ToolFilter.GrantAllows(mode, grant))
                return NotAllowedForConnection(call.Name, axis, access);

            // Consent is checked per call rather than by filtering the list the
            // model is offered, because a grant can be withdrawn mid-conversation
            // and the tool specs were chosen a turn ago.
            if (await ConsentRefusalAsync(call.Name, context.UserId, axis, access, cancellationToken) is { } refused)
                return refused;
        }

        var result = await _tasks.RunAsync(call.Name, args, callContext, cancellationToken);
        if (result.IsError) return Failed(call.Name, result.Text);

        return new AiToolRun(new AiToolInvocation(call.Name, result.Text, true), Cards(call.Name, result.Cards));
    }

    public async Task<AiToolInvocation> RunOnboardingAsync(
        AiToolCall call, AiToolContext context, CancellationToken cancellationToken = default)
    {
        var tool = AiToolCatalogue.FindOnboarding(call.Name);
        if (tool is null) return Failed(call.Name, $"There is no tool called '{call.Name}'.").Invocation;

        if (Grant(context.UserId) is { } connection)
        {
            var needed = tool.Access == AiToolAccess.Write ? GrantAxes.WriteScope(tool.Axis) : GrantAxes.ReadScope(tool.Axis);
            if (!connection.Allows(needed)) return NotAllowedForConnection(call.Name, tool.Axis, tool.Access).Invocation;
        }

        if (await ConsentRefusalAsync(call.Name, context.UserId, tool.Axis, tool.Access, cancellationToken) is { } refused)
            return refused.Invocation;

        JsonElement args;
        try
        {
            using var document = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(call.Arguments) ? "{}" : call.Arguments);
            args = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Failed(call.Name, "The arguments were not valid JSON.").Invocation;
        }

        object? result;
        try
        {
            var request = tool.Build(args, context);
            result = await _mediator.Send(request, cancellationToken);
        }
        catch (ValidationException ex)
        {
            return Failed(call.Name, string.Join(" ", ex.Errors.Select(e => e.ErrorMessage))).Invocation;
        }
        catch (DomainException ex)
        {
            return Failed(call.Name, ex.Message).Invocation;
        }
        catch (Exception ex)
        {
            // An unexpected failure is ours, not the model's, so it is logged
            // and the model is told something generic rather than a stack trace.
            _logger.LogError(ex, "Tool {Tool} failed for user {UserId}", call.Name, context.UserId);
            return Failed(call.Name, "That could not be completed.").Invocation;
        }

        return new AiToolInvocation(call.Name, tool.Describe(result), true);
    }

    /// <summary>
    /// The connected app this assistant runs for, if any. A connected app's assistant is held to
    /// what that app was allowed to do: an app with no write access to nutrition cannot get the
    /// model to log food. The web app's own session carries no grant.
    /// </summary>
    private GrantContext? Grant(Guid userId) =>
        _currentUser is { } current && current.UserId == userId ? current.Grant : null;

    private ToolCallContext CallContext(Guid userId) => new(
        userId,
        ToolSurface.Assistant,
        _currentUser is { } current && current.UserId == userId ? current.Role ?? "user" : "user",
        Grant(userId));

    private static AiToolRun NotAllowedForConnection(string name, DataAxis axis, AiToolAccess access)
    {
        var action = access == AiToolAccess.Write ? "change" : "read";
        return Failed(
            name,
            $"This connection is not allowed to {action} the user's {Describe(axis)}. "
            + "Tell them they can change that on their MCP connections page.");
    }

    private async Task<AiToolRun?> ConsentRefusalAsync(
        string name, Guid userId, DataAxis axis, AiToolAccess access, CancellationToken cancellationToken)
    {
        var consent = await ConsentAsync(userId, cancellationToken);
        var permitted = access == AiToolAccess.Write ? consent.AllowsWrite(axis) : consent.Allows(axis);
        if (permitted) return null;

        // The model still reads why, so it can explain; the surface gets the typed requirement
        // and offers to grant exactly that switch.
        var verb = access == AiToolAccess.Write ? "change" : "read";
        var refused = Failed(
            name,
            $"The user has not given permission to {verb} their {Describe(axis)}. "
            + "Tell them in one sentence; they are shown a way to allow it.");
        return refused with { ConsentRequired = AiConsentRequirements.For(axis, access) };
    }

    /// <summary>
    /// The tool's cards, each through the validator. One that fails is dropped and logged: it is
    /// a bug in the tool, and the turn's answer stands without it.
    /// </summary>
    private IReadOnlyList<AiCardDto> Cards(string tool, IReadOnlyList<AiCardDraft>? drafts)
    {
        if (drafts is null) return [];
        var cards = new List<AiCardDto>();
        foreach (var draft in drafts)
        {
            var card = AiCardValidator.Validate(draft, out var reason);
            if (card is null)
            {
                _logger.LogWarning("Tool {Tool} built a {Type} card that failed its schema: {Reason}", tool, draft.Type, reason);
                continue;
            }
            cards.Add(card);
        }
        return cards;
    }

    private static AiToolRun Failed(string tool, string error) =>
        new(new AiToolInvocation(tool, string.Empty, false, error), []);

    private async Task<UserAiConsent> ConsentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stored = await _context.UserAiConsents.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        // No row means never asked, which means no.
        return stored ?? UserAiConsent.None(userId);
    }

    private static string Describe(DataAxis axis) => axis switch
    {
        DataAxis.Nutrition => "food and goals",
        DataAxis.Training => "training",
        DataAxis.Body => "body measurements",
        DataAxis.Planning => "meal plans",
        _ => "log",
    };
}
