using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// The owner makes a plan active or archives it. Activating archives the
/// owner's other active personal plan, so one plan is followed at a time. A
/// coach's draft is not set this way: the client accepts or declines it.
/// </summary>
public record SetMealPlanStatusCommand(Guid Id, string Status) : IRequest<Unit>;

public class SetMealPlanStatusCommandValidator : AbstractValidator<SetMealPlanStatusCommand>
{
    public SetMealPlanStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<PlanStatus>(s, ignoreCase: true, out var parsed) && parsed != PlanStatus.Draft)
            .WithMessage("Status must be Active or Archived.");
    }
}

public class SetMealPlanStatusCommandHandler : IRequestHandler<SetMealPlanStatusCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public SetMealPlanStatusCommandHandler(IMizanDbContext context, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(SetMealPlanStatusCommand request, CancellationToken cancellationToken)
    {
        var plan = await _access.LoadEditableAsync(request.Id, cancellationToken);
        if (plan.Kind == PlanKind.Template)
            throw new DomainValidationException("A template has no status.");
        if (plan.Status == PlanStatus.Draft)
            throw new DomainValidationException("Accept or decline the draft instead.");

        var status = PlanRulesMapping.ParseEnum(request.Status, PlanStatus.Active);
        if (status == PlanStatus.Active) await _access.ActivateAsync(_cache, plan, cancellationToken);
        else plan.Status = PlanStatus.Archived;
        plan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return Unit.Value;
    }
}
