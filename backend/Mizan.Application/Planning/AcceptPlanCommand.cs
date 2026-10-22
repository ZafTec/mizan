using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>The client accepts a coach's draft: it becomes the active plan and the previous active plan is archived. The coach is told.</summary>
public record AcceptPlanCommand(Guid Id) : IRequest<Unit>;

public class AcceptPlanCommandHandler : IRequestHandler<AcceptPlanCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly INotificationWriter _notifications;
    private readonly HybridCache _cache;

    public AcceptPlanCommandHandler(IMizanDbContext context, PlanAccess access, INotificationWriter notifications, HybridCache cache)
    {
        _context = context;
        _access = access;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task<Unit> Handle(AcceptPlanCommand request, CancellationToken cancellationToken)
    {
        var userId = _access.RequireUserId();
        var plan = await _access.LoadReadableAsync(request.Id, cancellationToken);
        DraftDecision.EnsureClientCanDecide(plan, userId);

        await _access.ActivateAsync(_cache, plan, cancellationToken);
        plan.UpdatedAt = DateTime.UtcNow;
        await DraftDecision.TellCoachAsync(_context, _notifications, plan, userId, accepted: true, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>The rules both decisions share.</summary>
public static class DraftDecision
{
    public static void EnsureClientCanDecide(MealPlan plan, Guid userId)
    {
        if (plan.UserId != userId)
            throw new ForbiddenAccessException("Only the person the plan is for can accept or decline it.");
        if (plan.Status != PlanStatus.Draft || plan.AuthoredByUserId is null)
            throw new DomainValidationException("This plan is not a draft waiting for a decision.");
    }

    public static async Task TellCoachAsync(
        IMizanDbContext context, INotificationWriter notifications, MealPlan plan, Guid clientId, bool accepted, CancellationToken cancellationToken)
    {
        if (plan.AuthoredByUserId is not { } coachId) return;
        var clientName = await context.Users.AsNoTracking().Where(u => u.Id == clientId).Select(u => u.Name).FirstOrDefaultAsync(cancellationToken)
            ?? "Your client";
        var planName = string.IsNullOrWhiteSpace(plan.Name) ? "the meal plan" : $"\"{plan.Name}\"";
        await notifications.AddAsync(
            coachId,
            accepted ? "meal_plan_accepted" : "meal_plan_declined",
            accepted ? $"{clientName} accepted {planName}" : $"{clientName} declined {planName}",
            linkUrl: $"/trainer/clients/{clientId}/plan",
            cancellationToken: cancellationToken);
    }
}
