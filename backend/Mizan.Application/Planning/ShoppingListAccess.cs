using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Planning;

/// <summary>
/// Who may see and who may change a shopping list. The owner and every member
/// of the list's household read it. The owner writes to it; a household member
/// writes only with <see cref="HouseholdMember.CanEditShoppingList"/>, which the
/// household sets and nothing enforced before this.
/// </summary>
public sealed class ShoppingListAccess
{
    private readonly IMizanDbContext _context;
    private readonly IHouseholdAccess _households;
    private readonly ICurrentUserService _currentUser;

    public ShoppingListAccess(IMizanDbContext context, IHouseholdAccess households, ICurrentUserService currentUser)
    {
        _context = context;
        _households = households;
        _currentUser = currentUser;
    }

    public Guid RequireUserId() =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");

    public async Task<ShoppingList> LoadReadableAsync(Guid listId, CancellationToken cancellationToken)
    {
        RequireUserId();
        var list = await _context.ShoppingLists.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == listId, cancellationToken);
        if (list is null || !await _households.CanAccessRecordAsync(list.UserId, list.HouseholdId, cancellationToken))
            throw new EntityNotFoundException("Shopping list", listId);
        return list;
    }

    public async Task<ShoppingList> LoadEditableAsync(Guid listId, CancellationToken cancellationToken)
    {
        var list = await LoadReadableAsync(listId, cancellationToken);
        if (!await CanEditAsync(list, cancellationToken))
            throw new ForbiddenAccessException("Only the list's owner or a household member allowed to edit lists can change it.");
        return list;
    }

    public async Task<ShoppingListItem> LoadEditableItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        RequireUserId();
        var item = await _context.ShoppingListItems.Include(i => i.ShoppingList).FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null || !await _households.CanAccessRecordAsync(item.ShoppingList.UserId, item.ShoppingList.HouseholdId, cancellationToken))
            throw new EntityNotFoundException("Shopping list item", itemId);
        if (!await CanEditAsync(item.ShoppingList, cancellationToken))
            throw new ForbiddenAccessException("Only the list's owner or a household member allowed to edit lists can change it.");
        return item;
    }

    public async Task<bool> CanEditAsync(ShoppingList list, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return false;
        if (list.UserId == userId) return true;
        if (list.HouseholdId is not { } householdId) return false;
        if (!await _households.CanAccessAsync(householdId, cancellationToken)) return false;
        return await _context.HouseholdMembers.AnyAsync(
            m => m.HouseholdId == householdId && m.UserId == userId && m.CanEditShoppingList, cancellationToken);
    }

    public static async Task InvalidateAsync(HybridCache cache, ShoppingList list, CancellationToken cancellationToken)
    {
        await cache.RemoveByTagAsync(CacheTags.ShoppingList(list.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheTags.ShoppingLists(list.UserId), cancellationToken);
        if (list.HouseholdId is { } household)
            await cache.RemoveByTagAsync(CacheTags.HouseholdShoppingLists(household), cancellationToken);
    }
}
