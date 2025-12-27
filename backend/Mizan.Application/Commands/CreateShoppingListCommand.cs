using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateShoppingListCommand(string Name, Guid UserId, Guid? HouseholdId) : IRequest<Guid>;

public class CreateShoppingListCommandHandler : IRequestHandler<CreateShoppingListCommand, Guid>
{
    private readonly IMizanDbContext _context;

    public CreateShoppingListCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateShoppingListCommand request, CancellationToken cancellationToken)
    {
        var shoppingList = new ShoppingList
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            UserId = request.UserId,
            HouseholdId = request.HouseholdId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ShoppingLists.Add(shoppingList);
        await _context.SaveChangesAsync(cancellationToken);

        return shoppingList.Id;
    }
}
