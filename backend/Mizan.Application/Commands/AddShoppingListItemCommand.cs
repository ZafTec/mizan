using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record AddShoppingListItemCommand(Guid ShoppingListId, string ItemName, decimal? Amount, string? Unit, string? Category) : IRequest<Guid?>;

public class AddShoppingListItemCommandHandler : IRequestHandler<AddShoppingListItemCommand, Guid?>
{
    private readonly IMizanDbContext _context;
    private readonly IHouseholdAccess _households;
    private readonly ICurrentUserService _currentUser;

    public AddShoppingListItemCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, IHouseholdAccess households)
    {
        _households = households;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Guid?> Handle(AddShoppingListItemCommand request, CancellationToken cancellationToken)
    {
        var shoppingList = await _context.ShoppingLists.FindAsync(new object[] { request.ShoppingListId }, cancellationToken);
        if (shoppingList == null)
        {
            return null;
        }

        // Authorization: User must own the list OR be a member of the household
        if (!await IsAuthorizedAsync(shoppingList, cancellationToken))
        {
            return null;
        }

        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = request.ShoppingListId,
            ItemName = request.ItemName,
            Amount = request.Amount,
            Unit = request.Unit,
            Category = request.Category,
            IsChecked = false
        };

        _context.ShoppingListItems.Add(item);

        shoppingList.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return item.Id;
    }

    private async Task<bool> IsAuthorizedAsync(ShoppingList shoppingList, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            return false;
        }

        // User owns the list
        return await _households.CanAccessRecordAsync(shoppingList.UserId, shoppingList.HouseholdId, cancellationToken);
    }
}
