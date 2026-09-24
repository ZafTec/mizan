using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Common;

/// <summary>
/// Who owns a household. A member with the "owner" role is the owner. Until a
/// household has one, its creator is the owner while still a member: creation
/// has always given the creator the "admin" role, so this keeps existing
/// creators from being stranded without letting another admin gain owner-only
/// rights such as deletion.
/// </summary>
public static class HouseholdOwnership
{
    public static Guid? OwnerId(Household household, IReadOnlyCollection<HouseholdMember> members)
    {
        var explicitOwner = members.FirstOrDefault(m => m.Role == "owner");
        if (explicitOwner is not null) return explicitOwner.UserId;
        return members.Any(m => m.UserId == household.CreatedBy) ? household.CreatedBy : null;
    }
}

/// <summary>
/// What deleting a household would remove, read on the server. Every shopping
/// list and meal plan attached to the household counts: neither has an active
/// or archived state, so none is treated as disposable without being shown.
/// <see cref="Version"/> changes whenever the members, lists, plans or their
/// entries change, which is how a confirmation made against an older preview
/// is detected.
/// </summary>
public sealed record HouseholdDeletionSnapshot(
    Household Household,
    Guid? OwnerId,
    IReadOnlyList<HouseholdMember> Members,
    IReadOnlyList<Guid> ShoppingListIds,
    int ShoppingListItemCount,
    IReadOnlyList<Guid> MealPlanIds,
    int MealPlanRecipeCount,
    int PendingInvitationCount,
    string Version)
{
    public int OtherMemberCount(Guid userId) => Members.Count(m => m.UserId != userId);
    public bool HasPlans => ShoppingListIds.Count > 0 || MealPlanIds.Count > 0;

    public static async Task<HouseholdDeletionSnapshot> ReadAsync(
        IMizanDbContext context, Household household, CancellationToken cancellationToken)
    {
        var members = await context.HouseholdMembers
            .Where(m => m.HouseholdId == household.Id)
            .OrderBy(m => m.UserId)
            .ToListAsync(cancellationToken);
        var lists = await context.ShoppingLists
            .Where(l => l.HouseholdId == household.Id)
            .OrderBy(l => l.Id)
            .Select(l => new { l.Id, Items = l.Items.Count })
            .ToListAsync(cancellationToken);
        var plans = await context.MealPlans
            .Where(p => p.HouseholdId == household.Id)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Recipes = p.MealPlanRecipes.Count })
            .ToListAsync(cancellationToken);
        var pendingInvitations = await context.HouseholdInvitations
            .CountAsync(i => i.HouseholdId == household.Id && i.Status == "pending", cancellationToken);

        var fingerprint = new StringBuilder()
            .AppendJoin(',', members.Select(m => $"{m.UserId}:{m.Role}")).Append('|')
            .AppendJoin(',', lists.Select(l => $"{l.Id}:{l.Items}")).Append('|')
            .AppendJoin(',', plans.Select(p => $"{p.Id}:{p.Recipes}"))
            .ToString();
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)))[..16];

        return new HouseholdDeletionSnapshot(
            household,
            HouseholdOwnership.OwnerId(household, members),
            members,
            lists.Select(l => l.Id).ToList(),
            lists.Sum(l => l.Items),
            plans.Select(p => p.Id).ToList(),
            plans.Sum(p => p.Recipes),
            pendingInvitations,
            version.ToLowerInvariant());
    }
}
