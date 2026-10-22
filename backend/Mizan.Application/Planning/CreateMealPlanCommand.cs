using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>A person starts a plan: dates, rules, and any entries and cook sessions they already know.</summary>
public record CreateMealPlanCommand : CreateMealPlanRequest, IRequest<MealPlanCreatedResult>;

public class CreateMealPlanCommandValidator : AbstractValidator<CreateMealPlanCommand>
{
    public CreateMealPlanCommandValidator()
    {
        RuleFor(x => x.Name).MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotNull().WithMessage("A plan needs a start date.");
        RuleFor(x => x.DurationDays).InclusiveBetween(1, 31).WithMessage("A plan runs for 1 to 31 days.");
        RuleFor(x => x.Rules!).SetValidator(new PlanRulesDtoValidator()).When(x => x.Rules is not null);
        RuleForEach(x => x.Entries).SetValidator(new PlanEntryRequestValidator());
        RuleForEach(x => x.PrepSessions).SetValidator(new PrepSessionRequestValidator());
        RuleForEach(x => x.Entries).Must((cmd, e) => e.DayIndex < cmd.DurationDays)
            .WithMessage("An entry's day must be inside the plan.");
        RuleForEach(x => x.PrepSessions).Must((cmd, s) => s.DayIndex < cmd.DurationDays)
            .WithMessage("A prep session's day must be inside the plan.");
    }
}

public class CreateMealPlanCommandHandler : IRequestHandler<CreateMealPlanCommand, MealPlanCreatedResult>
{
    private readonly IMizanDbContext _context;
    private readonly IHouseholdAccess _households;
    private readonly PlanAccess _access;
    private readonly PlanEntryBuilder _entries;
    private readonly HybridCache _cache;

    public CreateMealPlanCommandHandler(IMizanDbContext context, IHouseholdAccess households, PlanAccess access, PlanEntryBuilder entries, HybridCache cache)
    {
        _context = context;
        _households = households;
        _access = access;
        _entries = entries;
        _cache = cache;
    }

    public async Task<MealPlanCreatedResult> Handle(CreateMealPlanCommand request, CancellationToken cancellationToken)
    {
        var userId = _access.RequireUserId();

        // Without this a plan could be filed under someone else's household.
        if (request.HouseholdId.HasValue && !await _households.CanAccessAsync(request.HouseholdId.Value, cancellationToken))
            throw new ForbiddenAccessException("You are not a member of this household");

        var plan = await PlanFactory.BuildAsync(_entries, request, userId, request.HouseholdId, authoredBy: null, PlanStatus.Active, cancellationToken);
        _context.MealPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);

        return new MealPlanCreatedResult(plan.Id, plan.Name, plan.Status.ToString(), plan.Entries.Count);
    }
}

/// <summary>Builds a plan with its sessions and entries from one request. Shared by the person's own create, the coach's draft and a template.</summary>
public static class PlanFactory
{
    public static async Task<MealPlan> BuildAsync(
        PlanEntryBuilder entries,
        CreateMealPlanRequest request,
        Guid? userId,
        Guid? householdId,
        Guid? authoredBy,
        PlanStatus status,
        CancellationToken cancellationToken,
        PlanKind kind = PlanKind.Personal)
    {
        var now = DateTime.UtcNow;
        var plan = new MealPlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            HouseholdId = householdId,
            AuthoredByUserId = authoredBy,
            Kind = kind,
            Status = status,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            StartDate = kind == PlanKind.Template ? null : request.StartDate,
            DurationDays = request.DurationDays,
            Rules = PlanRulesMapping.ToDomain(request.Rules),
            CreatedAt = now,
            UpdatedAt = now,
        };

        var sessionIds = new List<Guid>();
        foreach (var session in request.PrepSessions)
        {
            var row = new PrepSession
            {
                Id = Guid.NewGuid(),
                MealPlanId = plan.Id,
                DayIndex = session.DayIndex,
                Title = session.Title.Trim(),
                Notes = string.IsNullOrWhiteSpace(session.Notes) ? null : session.Notes.Trim(),
            };
            plan.PrepSessions.Add(row);
            sessionIds.Add(row.Id);
        }

        foreach (var entry in await entries.BuildAsync(plan, request.Entries, sessionIds, cancellationToken))
            plan.Entries.Add(entry);

        return plan;
    }
}
