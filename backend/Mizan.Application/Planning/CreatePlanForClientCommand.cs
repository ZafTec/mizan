using Mizan.Application.Notifications;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
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
        var trainerId = await CoachDrafts.RequirePlanningGrantAsync(_trainers, request.ClientId, cancellationToken);
        var plan = await PlanFactory.BuildAsync(_entries, request.Plan, request.ClientId, householdId: null, authoredBy: trainerId, PlanStatus.Draft, cancellationToken);
        return await CoachDrafts.SaveAsync(_context, _notifications, _cache, trainerId, plan, cancellationToken);
    }
}

/// <summary>
/// What every draft a coach makes for a client shares, however it was filled:
/// the grant it needs, and the notice the client gets when it is saved.
/// </summary>
public static class CoachDrafts
{
    /// <summary>The coach's id, once the relationship is active and the client allows meal planning.</summary>
    public static async Task<Guid> RequirePlanningGrantAsync(ITrainerAuthorizationService trainers, Guid clientId, CancellationToken cancellationToken)
    {
        await trainers.EnsureTrainerAccessAsync(cancellationToken);
        var trainerId = trainers.GetCurrentUserId();
        var relationship = await trainers.GetRelationshipForCurrentTrainerAndClientAsync(clientId, requireActive: true, cancellationToken);
        if (!relationship.CanPlanMeals)
            throw new ForbiddenAccessException("This client has not allowed you to plan their meals.");
        return trainerId;
    }

    /// <summary>Saves the draft and tells its owner, the client, that it waits for them.</summary>
    public static async Task<MealPlanCreatedResult> SaveAsync(
        IMizanDbContext context, INotificationWriter notifications, HybridCache cache, Guid trainerId, MealPlan plan, CancellationToken cancellationToken)
    {
        var clientId = plan.UserId ?? throw new InvalidOperationException("A coach's draft belongs to the client.");
        context.MealPlans.Add(plan);

        var coachName = await context.Users.AsNoTracking().Where(u => u.Id == trainerId).Select(u => u.Name).FirstOrDefaultAsync(cancellationToken);
        await notifications.SendAsync(
            new NotificationSend(clientId, "meal_plan_drafted", NotificationMessages.PlanDrafted(coachName ?? "Your coach", plan), $"/meal-plan/{plan.Id}"),
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(cache, plan, cancellationToken);
        return new MealPlanCreatedResult(plan.Id, plan.Name, plan.Status.ToString(), plan.Entries.Count);
    }
}
