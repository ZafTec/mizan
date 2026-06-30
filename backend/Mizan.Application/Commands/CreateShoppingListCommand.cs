using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateShoppingListCommand(string Name, Guid UserId, Guid? HouseholdId) : IRequest<Guid>;

public class CreateShoppingListCommandHandler : IRequestHandler<CreateShoppingListCommand, Guid>
{
    private const int FreeShoppingListLimit = 1;

    private readonly IMizanDbContext _context;
    private readonly IEntitlementService _entitlements;

    public CreateShoppingListCommandHandler(IMizanDbContext context, IEntitlementService entitlements)
    {
        _context = context;
        _entitlements = entitlements;
    }

    public async Task<Guid> Handle(CreateShoppingListCommand request, CancellationToken cancellationToken)
    {
        var entitlement = await _entitlements.GetAsync(request.UserId, cancellationToken);
        if (!entitlement.IsPro)
        {
            var existing = await _context.ShoppingLists.CountAsync(s => s.UserId == request.UserId, cancellationToken);
            if (existing >= FreeShoppingListLimit)
            {
                throw new ForbiddenAccessException("Free plan is limited to 1 shopping list. Upgrade to Pro for unlimited shopping lists.");
            }
        }

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
