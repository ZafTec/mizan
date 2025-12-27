using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetShoppingListQuery(Guid ShoppingListId) : IRequest<ShoppingListDto?>;

public record ShoppingListDto(
    Guid Id,
    string? Name,
    Guid UserId,
    Guid? HouseholdId,
    List<ShoppingListItemDto> Items
);

public record ShoppingListItemDto(
    Guid Id,
    string ItemName,
    decimal? Amount,
    string? Unit,
    string? Category,
    bool IsChecked
);

public class GetShoppingListQueryHandler : IRequestHandler<GetShoppingListQuery, ShoppingListDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetShoppingListQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ShoppingListDto?> Handle(GetShoppingListQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.ShoppingLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == request.ShoppingListId, cancellationToken);

        if (list == null)
        {
            return null;
        }

        // Authorization: User must own the list OR be a member of the household
        if (!await IsAuthorizedAsync(list, cancellationToken))
        {
            return null;
        }

        return new ShoppingListDto(
            list.Id,
            list.Name,
            list.UserId,
            list.HouseholdId,
            list.Items.Select(i => new ShoppingListItemDto(
                i.Id,
                i.ItemName,
                i.Amount,
                i.Unit,
                i.Category,
                i.IsChecked
            )).ToList()
        );
    }

    private async Task<bool> IsAuthorizedAsync(Domain.Entities.ShoppingList list, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            return false;
        }

        // User owns the list
        if (list.UserId == userId.Value)
        {
            return true;
        }

        // List belongs to a household and user is a member
        if (list.HouseholdId.HasValue)
        {
            var isMember = await _context.HouseholdMembers
                .AnyAsync(hm => hm.HouseholdId == list.HouseholdId.Value && hm.UserId == userId.Value, cancellationToken);
            return isMember;
        }

        return false;
    }
}
