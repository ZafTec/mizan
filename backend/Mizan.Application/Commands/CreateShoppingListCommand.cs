using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateShoppingListCommand(string Name, Guid UserId, Guid? HouseholdId) : IRequest<Guid>;

public class CreateShoppingListCommandHandler : IRequestHandler<CreateShoppingListCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly IHouseholdAccess _households;

    public CreateShoppingListCommandHandler(IMizanDbContext context, IHouseholdAccess households)
    {
        _households = households;
        _context = context;
    }

    public async Task<Guid> Handle(CreateShoppingListCommand request, CancellationToken cancellationToken)
    {
        // Without this a list could be filed under someone else's household.
        if (request.HouseholdId.HasValue && !await _households.CanAccessAsync(request.HouseholdId.Value, cancellationToken))
            throw new ForbiddenAccessException("You are not a member of this household");

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
