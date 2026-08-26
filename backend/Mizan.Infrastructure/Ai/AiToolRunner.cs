using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

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
    private readonly ILogger<AiToolRunner> _logger;

    public AiToolRunner(IMediator mediator, ILogger<AiToolRunner> logger)
    {
        _mediator = mediator;
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
}
