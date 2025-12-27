using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record ToggleShoppingListItemCommand(Guid ItemId, bool IsChecked) : IRequest<bool>;

public class ToggleShoppingListItemCommandHandler : IRequestHandler<ToggleShoppingListItemCommand, bool>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ToggleShoppingListItemCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(ToggleShoppingListItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.ShoppingListItems
            .Include(i => i.ShoppingList)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken);

        if (item == null)
        {
            return false;
        }

        // Authorization: User must own the list OR be a member of the household
        if (!await IsAuthorizedAsync(item.ShoppingList, cancellationToken))
        {
            return false;
        }

        item.IsChecked = request.IsChecked;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<bool> IsAuthorizedAsync(ShoppingList shoppingList, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            return false;
        }

        // User owns the list
        if (shoppingList.UserId == userId.Value)
        {
            return true;
        }

        // List belongs to a household and user is a member
        if (shoppingList.HouseholdId.HasValue)
        {
            var isMember = await _context.HouseholdMembers
                .AnyAsync(hm => hm.HouseholdId == shoppingList.HouseholdId.Value && hm.UserId == userId.Value, cancellationToken);
            return isMember;
        }

        return false;
    }
}
