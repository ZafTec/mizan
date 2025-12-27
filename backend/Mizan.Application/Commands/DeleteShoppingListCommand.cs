using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record DeleteShoppingListCommand(Guid Id) : IRequest<DeleteShoppingListResult>;

public record DeleteShoppingListResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class DeleteShoppingListCommandHandler : IRequestHandler<DeleteShoppingListCommand, DeleteShoppingListResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteShoppingListCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteShoppingListResult> Handle(DeleteShoppingListCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var shoppingList = await _context.ShoppingLists
            .Include(sl => sl.Items)
            .FirstOrDefaultAsync(sl => sl.Id == request.Id, cancellationToken);

        if (shoppingList == null)
        {
            return new DeleteShoppingListResult
            {
                Success = false,
                Message = "Shopping list not found or access denied"
            };
        }

        // Authorization: User must own the list OR be a member of the household
        if (!await IsAuthorizedAsync(shoppingList, cancellationToken))
        {
            return new DeleteShoppingListResult
            {
                Success = false,
                Message = "Shopping list not found or access denied"
            };
        }

        _context.ShoppingListItems.RemoveRange(shoppingList.Items);
        _context.ShoppingLists.Remove(shoppingList);
        await _context.SaveChangesAsync(cancellationToken);

        return new DeleteShoppingListResult
        {
            Success = true,
            Message = "Shopping list deleted successfully"
        };
    }

    private async Task<bool> IsAuthorizedAsync(Domain.Entities.ShoppingList shoppingList, CancellationToken cancellationToken)
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
