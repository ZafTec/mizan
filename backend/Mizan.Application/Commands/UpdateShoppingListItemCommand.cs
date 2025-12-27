using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record UpdateShoppingListItemCommand : IRequest<UpdateShoppingListItemResult>
{
    public Guid ItemId { get; init; }
    public bool? IsChecked { get; init; }
    public string? ItemName { get; init; }
    public decimal? Amount { get; init; }
    public string? Unit { get; init; }
    public string? Category { get; init; }
}

public record UpdateShoppingListItemResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class UpdateShoppingListItemCommandHandler : IRequestHandler<UpdateShoppingListItemCommand, UpdateShoppingListItemResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateShoppingListItemCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateShoppingListItemResult> Handle(UpdateShoppingListItemCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var item = await _context.ShoppingListItems
            .Include(i => i.ShoppingList)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken);

        if (item == null)
        {
            return new UpdateShoppingListItemResult
            {
                Success = false,
                Message = "Item not found or access denied"
            };
        }

        // Authorization: User must own the list OR be a member of the household
        if (!await IsAuthorizedAsync(item.ShoppingList, cancellationToken))
        {
            return new UpdateShoppingListItemResult
            {
                Success = false,
                Message = "Item not found or access denied"
            };
        }

        if (request.IsChecked.HasValue)
            item.IsChecked = request.IsChecked.Value;
        if (request.ItemName != null)
            item.ItemName = request.ItemName;
        if (request.Amount.HasValue)
            item.Amount = request.Amount;
        if (request.Unit != null)
            item.Unit = request.Unit;
        if (request.Category != null)
            item.Category = request.Category;

        item.ShoppingList.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateShoppingListItemResult
        {
            Success = true,
            Message = "Item updated successfully"
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
