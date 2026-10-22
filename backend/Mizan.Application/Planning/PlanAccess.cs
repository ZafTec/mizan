using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// The one answer to "may the caller see or change this plan". A personal plan
/// is read by its owner, by the coach who drafted it, and by the members of
/// its household; it is edited by the owner and the household's members, and
/// by the coach only while it is a draft. A global template (no owner) is read
/// by everyone and edited by administrators; a coach's template by the coach.
/// Every planning handler goes through here, so the rule lives once.
/// </summary>
public sealed class PlanAccess
{
    private readonly IMizanDbContext _context;
    private readonly IHouseholdAccess _households;
    private readonly ICurrentUserService _currentUser;

    public PlanAccess(IMizanDbContext context, IHouseholdAccess households, ICurrentUserService currentUser)
    {
        _context = context;
        _households = households;
        _currentUser = currentUser;
    }

    public Guid RequireUserId() =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");

    public bool IsAdmin => _currentUser.IsInRole("admin");

    /// <summary>Loads a plan the caller may read, with its entries and sessions. Anyone else gets "not found".</summary>
    public async Task<MealPlan> LoadReadableAsync(Guid planId, CancellationToken cancellationToken)
    {
        RequireUserId();
        var plan = await _context.MealPlans
            .Include(p => p.Entries)
            .Include(p => p.PrepSessions)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
        if (plan is null || !await CanReadAsync(plan, cancellationToken))
            throw new EntityNotFoundException("Meal plan", planId);
        return plan;
    }

    /// <summary>Loads a plan the caller may change. A reader who may not write gets "forbidden"; a stranger "not found".</summary>
    public async Task<MealPlan> LoadEditableAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plan = await LoadReadableAsync(planId, cancellationToken);
        if (!await CanEditAsync(plan, cancellationToken))
            throw new ForbiddenAccessException(plan.Kind == PlanKind.Template
                ? "Only the template's owner can change it."
                : plan.Status == PlanStatus.Draft && plan.AuthoredByUserId is not null && plan.UserId == _currentUser.UserId
                    ? "Accept the draft before changing it, or ask your coach to."
                    : "Only the plan's owner, its household, or its coach while it is a draft can change it.");
        return plan;
    }

    public async Task<bool> CanReadAsync(MealPlan plan, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return false;
        if (plan.Kind == PlanKind.Template)
            return plan.UserId is null || plan.UserId == userId;
        if (plan.AuthoredByUserId == userId) return true;
        if (plan.UserId is not { } ownerId) return false;
        return await _households.CanAccessRecordAsync(ownerId, plan.HouseholdId, cancellationToken);
    }

    public async Task<bool> CanEditAsync(MealPlan plan, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return false;
        if (plan.Kind == PlanKind.Template)
            return plan.UserId is null ? IsAdmin : plan.UserId == userId;
        if (plan.AuthoredByUserId == userId && plan.UserId != userId)
            return plan.Status == PlanStatus.Draft;
        if (plan.Status == PlanStatus.Draft && plan.AuthoredByUserId is not null && plan.AuthoredByUserId != userId)
            return false;
        if (plan.UserId is not { } ownerId) return false;
        return await _households.CanAccessRecordAsync(ownerId, plan.HouseholdId, cancellationToken);
    }

    /// <summary>Every cached read that could show this plan: its detail, its owner's and author's listings, its household's listings.</summary>
    public static async Task InvalidateAsync(HybridCache cache, MealPlan plan, CancellationToken cancellationToken)
    {
        await cache.RemoveByTagAsync(CacheTags.MealPlan(plan.Id), cancellationToken);
        if (plan.UserId is { } owner) await cache.RemoveByTagAsync(CacheTags.MealPlansList(owner), cancellationToken);
        if (plan.AuthoredByUserId is { } author && author != plan.UserId)
            await cache.RemoveByTagAsync(CacheTags.MealPlansList(author), cancellationToken);
        if (plan.HouseholdId is { } household) await cache.RemoveByTagAsync(CacheTags.HouseholdPlans(household), cancellationToken);
    }

    /// <summary>
    /// A person follows one plan at a time: when this plan becomes active, the
    /// owner's other active personal plans are archived. Household plans are
    /// shared with people who have their own, so they are left alone.
    /// </summary>
    public async Task ActivateAsync(HybridCache cache, MealPlan plan, CancellationToken cancellationToken)
    {
        plan.Status = PlanStatus.Active;
        if (plan.HouseholdId is not null || plan.UserId is not { } owner) return;
        var others = await _context.MealPlans
            .Where(p => p.Id != plan.Id && p.UserId == owner && p.HouseholdId == null
                && p.Kind == PlanKind.Personal && p.Status == PlanStatus.Active)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var other in others)
        {
            other.Status = PlanStatus.Archived;
            other.UpdatedAt = now;
            await cache.RemoveByTagAsync(CacheTags.MealPlan(other.Id), cancellationToken);
        }
    }
}
