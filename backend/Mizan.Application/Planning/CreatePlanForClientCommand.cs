using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// A coach drafts a plan for a client. The relationship must be active and the
/// client must have granted <c>CanPlanMeals</c>. The plan belongs to the client,
/// names the coach as its author, and waits as a draft until the client accepts
/// or declines it. The client is told at once.
/// </summary>
public record CreatePlanForClientCommand(Guid ClientId, CreateMealPlanRequest Plan) : IRequest<MealPlanCreatedResult>;

public class CreatePlanForClientCommandValidator : AbstractValidator<CreatePlanForClientCommand>
{
    public CreatePlanForClientCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Plan).NotNull();
        RuleFor(x => x.Plan.Name).MaximumLength(255);
        RuleFor(x => x.Plan.Description).MaximumLength(2000);
        RuleFor(x => x.Plan.StartDate).NotNull().WithMessage("A plan needs a start date.");
        RuleFor(x => x.Plan.DurationDays).InclusiveBetween(1, 31).WithMessage("A plan runs for 1 to 31 days.");
        RuleFor(x => x.Plan.HouseholdId).Null().WithMessage("A coach's draft is personal; it is not filed under a household.");
        RuleFor(x => x.Plan.Rules!).SetValidator(new PlanRulesDtoValidator()).When(x => x.Plan.Rules is not null);
        RuleForEach(x => x.Plan.Entries).SetValidator(new PlanEntryRequestValidator());
        RuleForEach(x => x.Plan.PrepSessions).SetValidator(new PrepSessionRequestValidator());
        RuleForEach(x => x.Plan.Entries).Must((cmd, e) => e.DayIndex < cmd.Plan.DurationDays).WithMessage("An entry's day must be inside the plan.");
        RuleForEach(x => x.Plan.PrepSessions).Must((cmd, s) => s.DayIndex < cmd.Plan.DurationDays).WithMessage("A prep session's day must be inside the plan.");
    }
}

public class CreatePlanForClientCommandHandler : IRequestHandler<CreatePlanForClientCommand, MealPlanCreatedResult>
{
    private readonly IMizanDbContext _context;
    private readonly ITrainerAuthorizationService _trainers;
    private readonly PlanEntryBuilder _entries;
    private readonly INotificationWriter _notifications;
    private readonly HybridCache _cache;

    public CreatePlanForClientCommandHandler(
        IMizanDbContext context, ITrainerAuthorizationService trainers, PlanEntryBuilder entries, INotificationWriter notifications, HybridCache cache)
    {
        _context = context;
        _trainers = trainers;
        _entries = entries;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task<MealPlanCreatedResult> Handle(CreatePlanForClientCommand request, CancellationToken cancellationToken)
    {
        await _trainers.EnsureTrainerAccessAsync(cancellationToken);
        var trainerId = _trainers.GetCurrentUserId();
        var relationship = await _trainers.GetRelationshipForCurrentTrainerAndClientAsync(request.ClientId, requireActive: true, cancellationToken);
        if (!relationship.CanPlanMeals)
            throw new ForbiddenAccessException("This client has not allowed you to plan their meals.");

        var plan = await PlanFactory.BuildAsync(_entries, request.Plan, request.ClientId, householdId: null, authoredBy: trainerId, PlanStatus.Draft, cancellationToken);
        _context.MealPlans.Add(plan);

        var coachName = await _context.Users.AsNoTracking().Where(u => u.Id == trainerId).Select(u => u.Name).FirstOrDefaultAsync(cancellationToken);
        await _notifications.AddAsync(
            request.ClientId,
            "meal_plan_drafted",
            $"{coachName ?? "Your coach"} drafted a meal plan for you",
            string.IsNullOrWhiteSpace(plan.Name) ? "Open it to accept or decline." : $"\"{plan.Name}\" is waiting for you to accept or decline.",
            $"/meal-plan/{plan.Id}",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return new MealPlanCreatedResult(plan.Id, plan.Name, plan.Status.ToString(), plan.Entries.Count);
    }
}
