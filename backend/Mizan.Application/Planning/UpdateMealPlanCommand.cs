using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>Changes a plan's name, dates, length and rules. Entries are changed one at a time.</summary>
public record UpdateMealPlanCommand(Guid Id, UpdateMealPlanRequest Plan) : IRequest<Unit>;

public class UpdateMealPlanCommandValidator : AbstractValidator<UpdateMealPlanCommand>
{
    public UpdateMealPlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Plan.Name).MaximumLength(255);
        RuleFor(x => x.Plan.Description).MaximumLength(2000);
        RuleFor(x => x.Plan.DurationDays).InclusiveBetween(1, 31).WithMessage("A plan runs for 1 to 31 days.");
        RuleFor(x => x.Plan.Rules!).SetValidator(new PlanRulesDtoValidator()).When(x => x.Plan.Rules is not null);
    }
}

public class UpdateMealPlanCommandHandler : IRequestHandler<UpdateMealPlanCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public UpdateMealPlanCommandHandler(IMizanDbContext context, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(UpdateMealPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _access.LoadEditableAsync(request.Id, cancellationToken);
        if (plan.Kind == PlanKind.Template)
            throw new DomainValidationException("This is a template. Change it through the templates routes.");
        if (request.Plan.StartDate is null)
            throw new DomainValidationException("A plan needs a start date.");

        PlanResize.EnsureFits(plan, request.Plan.DurationDays);

        plan.Name = string.IsNullOrWhiteSpace(request.Plan.Name) ? null : request.Plan.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(request.Plan.Description) ? null : request.Plan.Description.Trim();
        plan.StartDate = request.Plan.StartDate;
        plan.DurationDays = request.Plan.DurationDays;
        if (request.Plan.Rules is not null) plan.Rules = PlanRulesMapping.ToDomain(request.Plan.Rules);
        plan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>A plan cannot be shortened past the days it already uses.</summary>
public static class PlanResize
{
    public static void EnsureFits(Domain.Entities.MealPlan plan, int durationDays)
    {
        var lastEntryDay = plan.Entries.Select(e => e.DayIndex).DefaultIfEmpty(-1).Max();
        var lastSessionDay = plan.PrepSessions.Select(s => s.DayIndex).DefaultIfEmpty(-1).Max();
        var lastUsed = Math.Max(lastEntryDay, lastSessionDay);
        if (lastUsed >= durationDays)
            throw new DomainValidationException(
                $"The plan has entries on day {lastUsed + 1}. Remove them before shortening it to {durationDays} day(s).");
    }
}
