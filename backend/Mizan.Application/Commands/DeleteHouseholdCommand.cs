using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

/// <summary>What the owner sees before confirming, and again when a confirmation is stale.</summary>
public record HouseholdDeletionPreview
{
    public Guid HouseholdId { get; init; }
    public string HouseholdName { get; init; } = string.Empty;
    public int OtherMemberCount { get; init; }
    public int ShoppingListCount { get; init; }
    public int ShoppingListItemCount { get; init; }
    public int MealPlanCount { get; init; }
    public int MealPlanRecipeCount { get; init; }
    public int PendingInvitationCount { get; init; }

    /// <summary>Send back with the deletion. Any change to members, lists or plans changes it.</summary>
    public string Version { get; init; } = string.Empty;

    internal static HouseholdDeletionPreview From(HouseholdDeletionSnapshot snapshot, Guid userId) => new()
    {
        HouseholdId = snapshot.Household.Id,
        HouseholdName = snapshot.Household.Name,
        OtherMemberCount = snapshot.OtherMemberCount(userId),
        ShoppingListCount = snapshot.ShoppingListIds.Count,
        ShoppingListItemCount = snapshot.ShoppingListItemCount,
        MealPlanCount = snapshot.MealPlanIds.Count,
        MealPlanRecipeCount = snapshot.MealPlanRecipeCount,
        PendingInvitationCount = snapshot.PendingInvitationCount,
        Version = snapshot.Version
    };
}

[JsonConverter(typeof(JsonStringEnumConverter<HouseholdDeletionStatus>))]
public enum HouseholdDeletionStatus
{
    /// <summary>Preview only: the owner may confirm with the returned version.</summary>
    Ready,
    Deleted,
    NotFound,
    NotOwner,
    HasOtherMembers,
    PlansNotConfirmed,
    Stale
}

public record HouseholdDeletionResult
{
    public HouseholdDeletionStatus Status { get; init; }
    public string? Message { get; init; }

    /// <summary>The current state, for every refusal after the caller is known to be the owner.</summary>
    public HouseholdDeletionPreview? Preview { get; init; }
}

public record GetHouseholdDeletionPreviewQuery(Guid HouseholdId, Guid UserId) : IRequest<HouseholdDeletionResult>;

public record DeleteHouseholdCommand(Guid HouseholdId, Guid UserId, string Version, bool DeletePlans) : IRequest<HouseholdDeletionResult>;

public class DeleteHouseholdCommandValidator : AbstractValidator<DeleteHouseholdCommand>
{
    public DeleteHouseholdCommandValidator()
    {
        RuleFor(x => x.HouseholdId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty().MaximumLength(64);
    }
}

internal static class HouseholdDeletionRules
{
    public const string NotOwnerMessage = "Only the household owner can delete it.";

    /// <summary>Null when the caller may go on; otherwise the refusal.</summary>
    public static HouseholdDeletionResult? Refuse(HouseholdDeletionSnapshot snapshot, Guid userId)
    {
        if (snapshot.Members.All(m => m.UserId != userId))
            return new HouseholdDeletionResult { Status = HouseholdDeletionStatus.NotFound, Message = "Household not found." };
        if (snapshot.OwnerId != userId)
            return new HouseholdDeletionResult { Status = HouseholdDeletionStatus.NotOwner, Message = NotOwnerMessage };
        var others = snapshot.OtherMemberCount(userId);
        if (others > 0)
            return new HouseholdDeletionResult
            {
                Status = HouseholdDeletionStatus.HasOtherMembers,
                Message = $"Remove the {others} other {(others == 1 ? "member" : "members")} before deleting this household.",
                Preview = HouseholdDeletionPreview.From(snapshot, userId)
            };
        return null;
    }
}

public class GetHouseholdDeletionPreviewQueryHandler : IRequestHandler<GetHouseholdDeletionPreviewQuery, HouseholdDeletionResult>
{
    private readonly IMizanDbContext _context;

    public GetHouseholdDeletionPreviewQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<HouseholdDeletionResult> Handle(GetHouseholdDeletionPreviewQuery request, CancellationToken cancellationToken)
    {
        var household = await _context.Households.AsNoTracking().FirstOrDefaultAsync(h => h.Id == request.HouseholdId, cancellationToken);
        if (household is null)
            return new HouseholdDeletionResult { Status = HouseholdDeletionStatus.NotFound, Message = "Household not found." };

        var snapshot = await HouseholdDeletionSnapshot.ReadAsync(_context, household, cancellationToken);
        return HouseholdDeletionRules.Refuse(snapshot, request.UserId) ?? new HouseholdDeletionResult
        {
            Status = HouseholdDeletionStatus.Ready,
            Preview = HouseholdDeletionPreview.From(snapshot, request.UserId)
        };
    }
}

/// <summary>
/// Deletes a household for its sole owner. Everything is decided again inside
/// the transaction, with the household row locked, so a member who joins or a
/// plan created after the preview makes the confirmation stale rather than
/// being deleted or bypassing the member check. Only the household's shopping
/// lists and meal plans, their entries, its memberships and invitations are
/// removed; recipes lose their household link but keep their owner, and
/// nothing personal or belonging to another household is touched.
/// </summary>
public class DeleteHouseholdCommandHandler : IRequestHandler<DeleteHouseholdCommand, HouseholdDeletionResult>
{
    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public DeleteHouseholdCommandHandler(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<HouseholdDeletionResult> Handle(DeleteHouseholdCommand request, CancellationToken cancellationToken)
    {
        var removedPlans = new List<(Guid Id, Guid UserId)>();
        var result = await _context.ExecuteInTransactionAsync(async ct =>
        {
            var household = await _context.LockHouseholdAsync(request.HouseholdId, ct);
            if (household is null)
                return new HouseholdDeletionResult { Status = HouseholdDeletionStatus.NotFound, Message = "Household not found." };

            var snapshot = await HouseholdDeletionSnapshot.ReadAsync(_context, household, ct);
            var refusal = HouseholdDeletionRules.Refuse(snapshot, request.UserId);
            if (refusal is not null) return refusal;

            var preview = HouseholdDeletionPreview.From(snapshot, request.UserId);
            if (!string.Equals(snapshot.Version, request.Version, StringComparison.Ordinal))
                return new HouseholdDeletionResult
                {
                    Status = HouseholdDeletionStatus.Stale,
                    Message = "This household changed after you opened the confirmation. Review it again.",
                    Preview = preview
                };
            if (snapshot.HasPlans && !request.DeletePlans)
                return new HouseholdDeletionResult
                {
                    Status = HouseholdDeletionStatus.PlansNotConfirmed,
                    Message = "Confirm that the household's shopping lists and meal plans are deleted too.",
                    Preview = preview
                };

            var lists = await _context.ShoppingLists.Where(l => snapshot.ShoppingListIds.Contains(l.Id)).ToListAsync(ct);
            _context.ShoppingListItems.RemoveRange(
                await _context.ShoppingListItems.Where(i => snapshot.ShoppingListIds.Contains(i.ShoppingListId)).ToListAsync(ct));
            _context.ShoppingLists.RemoveRange(lists);

            var plans = await _context.MealPlans.Where(p => snapshot.MealPlanIds.Contains(p.Id)).ToListAsync(ct);
            _context.MealPlanRecipes.RemoveRange(
                await _context.MealPlanRecipes.Where(r => snapshot.MealPlanIds.Contains(r.MealPlanId)).ToListAsync(ct));
            _context.MealPlans.RemoveRange(plans);
            removedPlans.AddRange(plans.Select(p => (p.Id, p.UserId)));

            // Recipes stay with their owner; only the shared-household link goes.
            var now = DateTime.UtcNow;
            foreach (var recipe in await _context.Recipes.Where(r => r.HouseholdId == household.Id).ToListAsync(ct))
            {
                recipe.HouseholdId = null;
                recipe.UpdatedAt = now;
            }

            foreach (var preference in await _context.UserHouseholdPreferences.Where(p => p.ActiveHouseholdId == household.Id).ToListAsync(ct))
            {
                preference.ActiveHouseholdId = null;
                preference.UpdatedAt = now;
            }

            _context.HouseholdInvitations.RemoveRange(
                await _context.HouseholdInvitations.Where(i => i.HouseholdId == household.Id).ToListAsync(ct));
            _context.HouseholdMembers.RemoveRange(snapshot.Members);
            _context.Households.Remove(household);
            await _context.SaveChangesAsync(ct);

            return new HouseholdDeletionResult { Status = HouseholdDeletionStatus.Deleted };
        }, cancellationToken);

        if (result.Status == HouseholdDeletionStatus.Deleted)
        {
            foreach (var (id, userId) in removedPlans)
            {
                await _cache.RemoveByTagAsync(CacheTags.MealPlan(id), cancellationToken);
                await _cache.RemoveByTagAsync(CacheTags.MealPlansList(userId), cancellationToken);
            }
            await _cache.RemoveByTagAsync(CacheTags.Recipes, cancellationToken);
        }
        return result;
    }
}
