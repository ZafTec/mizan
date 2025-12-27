using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetShoppingListByIdQuery(Guid Id) : IRequest<ShoppingListDetailDto?>;

public record ShoppingListDetailDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public List<ShoppingListItemDetailDto> Items { get; init; } = new();
    public int TotalItems { get; init; }
    public int CheckedItems { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record ShoppingListItemDetailDto
{
    public Guid Id { get; init; }
    public Guid? FoodId { get; init; }
    public string? FoodName { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string? Unit { get; init; }
    public string? Category { get; init; }
    public bool IsChecked { get; init; }
}

public class GetShoppingListByIdQueryHandler : IRequestHandler<GetShoppingListByIdQuery, ShoppingListDetailDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetShoppingListByIdQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ShoppingListDetailDto?> Handle(GetShoppingListByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var shoppingList = await _context.ShoppingLists
            .Include(sl => sl.Items)
                .ThenInclude(i => i.Food)
            .FirstOrDefaultAsync(sl => sl.Id == request.Id && sl.UserId == _currentUser.UserId, cancellationToken);

        if (shoppingList == null)
        {
            return null;
        }

        return new ShoppingListDetailDto
        {
            Id = shoppingList.Id,
            Name = shoppingList.Name,
            Items = shoppingList.Items.Select(i => new ShoppingListItemDetailDto
            {
                Id = i.Id,
                FoodId = i.FoodId,
                FoodName = i.Food?.Name,
                ItemName = i.ItemName,
                Amount = i.Amount,
                Unit = i.Unit,
                Category = i.Category,
                IsChecked = i.IsChecked
            }).OrderBy(i => i.IsChecked).ThenBy(i => i.Category).ThenBy(i => i.ItemName).ToList(),
            TotalItems = shoppingList.Items.Count,
            CheckedItems = shoppingList.Items.Count(i => i.IsChecked),
            CreatedAt = shoppingList.CreatedAt,
            UpdatedAt = shoppingList.UpdatedAt
        };
    }
}
