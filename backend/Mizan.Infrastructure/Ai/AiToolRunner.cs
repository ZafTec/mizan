using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
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
/// for by a model (docs/REFOCUS.md §10).
/// </summary>
public class AiToolRunner : IAiToolRunner
{
    private readonly IMediator _mediator;
    private readonly IMizanDbContext _context;
    private readonly ILogger<AiToolRunner> _logger;

    public AiToolRunner(IMediator mediator, IMizanDbContext context, ILogger<AiToolRunner> logger)
    {
        _mediator = mediator;
        _context = context;
        _logger = logger;
    }

    public async Task<AiToolInvocation> RunAsync(
        AiToolCall call, AiToolContext context, CancellationToken cancellationToken = default)
    {
        var tool = AiToolCatalogue.Find(call.Name);
        if (tool is null)
        {
            // Not an error worth logging loudly: a model asking for a tool it
            // was not given is a model being told no, which is the system
            // working.
            return new AiToolInvocation(
                call.Name, string.Empty, false, $"There is no tool called '{call.Name}'.");
        }

        // Consent is checked per call rather than by filtering the list the
        // model is offered, because a grant can be withdrawn mid-conversation
        // and the tool specs were chosen a turn ago.
        var consent = await ConsentAsync(context.UserId, cancellationToken);
        var permitted = tool.Access == AiToolAccess.Write
            ? consent.AllowsWrite(tool.Axis)
            : consent.Allows(tool.Axis);

        if (!permitted)
        {
            var verb = tool.Access == AiToolAccess.Write ? "change" : "read";
            return new AiToolInvocation(
                call.Name, string.Empty, false,
                $"The user has not given permission to {verb} their {Describe(tool.Axis)}. "
                + "Tell them, and say they can grant it in Settings.");
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
            return new AiToolInvocation(call.Name, string.Empty, false, "The arguments were not valid JSON.");
        }

        try
        {
            var request = tool.Build(args, context);
            var result = await _mediator.Send(request, cancellationToken);
            return new AiToolInvocation(call.Name, tool.Describe(result), true);
        }
        catch (ValidationException ex)
        {
            return new AiToolInvocation(
                call.Name, string.Empty, false,
                string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (DomainException ex)
        {
            return new AiToolInvocation(call.Name, string.Empty, false, ex.Message);
        }
        catch (Exception ex)
        {
            // An unexpected failure is ours, not the model's, so it is logged
            // and the model is told something generic rather than a stack trace.
            _logger.LogError(ex, "Tool {Tool} failed for user {UserId}", call.Name, context.UserId);
            return new AiToolInvocation(call.Name, string.Empty, false, "That could not be completed.");
        }
    }

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
        _ => "log",
    };
}
