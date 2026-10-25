using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// A coach drafts a client's plan from a template: one of their own or a
/// global one. The copy is the client's, waits as a draft that names the
/// coach, and the client is told, exactly as a draft written by hand. The
/// template is not changed and stays the coach's.
/// </summary>
public record ApplyTemplateForClientCommand(Guid ClientId, Guid TemplateId, DateOnly StartDate, string? Name) : IRequest<MealPlanCreatedResult>;

public class ApplyTemplateForClientCommandValidator : AbstractValidator<ApplyTemplateForClientCommand>
{
    public ApplyTemplateForClientCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("A plan needs a start date.");
        RuleFor(x => x.Name).MaximumLength(255);
    }
}

public class ApplyTemplateForClientCommandHandler : IRequestHandler<ApplyTemplateForClientCommand, MealPlanCreatedResult>
{
    private readonly IMizanDbContext _context;
    private readonly ITrainerAuthorizationService _trainers;
    private readonly PlanAccess _access;
    private readonly INotificationWriter _notifications;
    private readonly HybridCache _cache;

    public ApplyTemplateForClientCommandHandler(
        IMizanDbContext context, ITrainerAuthorizationService trainers, PlanAccess access, INotificationWriter notifications, HybridCache cache)
    {
        _context = context;
        _trainers = trainers;
        _access = access;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task<MealPlanCreatedResult> Handle(ApplyTemplateForClientCommand request, CancellationToken cancellationToken)
    {
        var trainerId = await CoachDrafts.RequirePlanningGrantAsync(_trainers, request.ClientId, cancellationToken);
        var template = await _access.LoadReadableAsync(request.TemplateId, cancellationToken);
        if (template.Kind != PlanKind.Template)
            throw new EntityNotFoundException("Template", request.TemplateId);

        var plan = ApplyTemplateCommandHandler.Copy(
            template, request.ClientId, request.StartDate, request.Name, householdId: null, PlanStatus.Draft, authoredBy: trainerId);
        return await CoachDrafts.SaveAsync(_context, _notifications, _cache, trainerId, plan, cancellationToken);
    }
}
