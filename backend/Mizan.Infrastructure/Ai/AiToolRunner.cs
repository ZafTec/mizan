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
using Mizan.Contracts.Ai;
using Mizan.Domain.Ai;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// The only bridge between a model and a command.
///
/// It goes through <see cref="IMediator"/> rather than calling a handler, so
/// the pipeline the HTTP path uses applies unchanged: FluentValidation rejects
/// the same arguments, and AuditBehavior records the write with the same
/// attribution. A tool call is an ordinary command that happened to be asked
/// for by a model (docs/AI.md).
/// </summary>
public class AiToolRunner : IAiToolRunner
{
    private readonly IMediator _mediator;
    private readonly IMizanDbContext _context;
    private readonly ILogger<AiToolRunner> _logger;
    private readonly ICurrentUserService? _currentUser;

    public AiToolRunner(
        IMediator mediator, IMizanDbContext context, ILogger<AiToolRunner> logger,
        ICurrentUserService? currentUser = null)
    {
        _mediator = mediator;
        _context = context;
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<AiToolInvocation> RunAsync(
        AiToolCall call, AiToolContext context, CancellationToken cancellationToken = default) =>
        (await RunWithCardsAsync(call, context, cancellationToken)).Invocation;

    public async Task<AiToolRun> RunWithCardsAsync(
        AiToolCall call, AiToolContext context, CancellationToken cancellationToken = default)
    {
        var tool = AiToolCatalogue.Find(call.Name);
        if (tool is null)
        {
            // Not an error worth logging loudly: a model asking for a tool it
            // was not given is a model being told no, which is the system
            // working.
            return Failed(call.Name, $"There is no tool called '{call.Name}'.");
        }

        // Consent is checked per call rather than by filtering the list the
        // model is offered, because a grant can be withdrawn mid-conversation
        // and the tool specs were chosen a turn ago.
        // A connected app's assistant is held to what that app was allowed to do:
        // an app with no write access to nutrition cannot get the model to log food.
        if (_currentUser?.Grant is { } connection && _currentUser.UserId == context.UserId)
        {
            var needed = tool.Access == AiToolAccess.Write
                ? GrantAxes.WriteScope(tool.Axis)
                : GrantAxes.ReadScope(tool.Axis);
            if (!connection.Allows(needed))
            {
                var action = tool.Access == AiToolAccess.Write ? "change" : "read";
                return Failed(
                    call.Name,
                    $"This connection is not allowed to {action} the user's {Describe(tool.Axis)}. "
                    + "Tell them they can change that on their MCP connections page.");
            }
        }

        var consent = await ConsentAsync(context.UserId, cancellationToken);
        var permitted = tool.Access == AiToolAccess.Write
            ? consent.AllowsWrite(tool.Axis)
            : consent.Allows(tool.Axis);

        if (!permitted)
        {
            // The model still reads why, so it can explain; the surface gets the typed requirement
            // and offers to grant exactly that switch.
            var verb = tool.Access == AiToolAccess.Write ? "change" : "read";
            var refused = Failed(
                call.Name,
                $"The user has not given permission to {verb} their {Describe(tool.Axis)}. "
                + "Tell them in one sentence; they are shown a way to allow it.");
            return refused with { ConsentRequired = AiConsentRequirements.For(tool.Axis, tool.Access) };
        }

        JsonElement args;
        try
        {
            using var document = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(call.Arguments) ? "{}" : call.Arguments);
            args = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Failed(call.Name, "The arguments were not valid JSON.");
        }

        object? result;
        try
        {
            var request = tool.Build(args, context);
            result = await _mediator.Send(request, cancellationToken);
        }
        catch (ValidationException ex)
        {
            return Failed(call.Name, string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (DomainException ex)
        {
            return Failed(call.Name, ex.Message);
        }
        catch (Exception ex)
        {
            // An unexpected failure is ours, not the model's, so it is logged
            // and the model is told something generic rather than a stack trace.
            _logger.LogError(ex, "Tool {Tool} failed for user {UserId}", call.Name, context.UserId);
            return Failed(call.Name, "That could not be completed.");
        }

        return new AiToolRun(new AiToolInvocation(call.Name, tool.Describe(result), true), Cards(tool, result));
    }

    /// <summary>
    /// The tool's cards, each through the validator. One that fails is dropped and logged: it is
    /// a bug in the tool, and the turn's answer stands without it.
    /// </summary>
    private IReadOnlyList<AiCardDto> Cards(AiToolDefinition tool, object? result)
    {
        if (tool.Cards is null) return [];
        var cards = new List<AiCardDto>();
        foreach (var draft in tool.Cards(result))
        {
            var card = AiCardValidator.Validate(draft, out var reason);
            if (card is null)
            {
                _logger.LogWarning("Tool {Tool} built a {Type} card that failed its schema: {Reason}", tool.Name, draft.Type, reason);
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
