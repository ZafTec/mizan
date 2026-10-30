using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Planning;

public record DeleteShoppingListItemCommand(Guid ItemId) : IRequest<Unit>;

public class DeleteShoppingListItemCommandHandler : IRequestHandler<DeleteShoppingListItemCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ShoppingListAccess _access;
    private readonly HybridCache _cache;

    public DeleteShoppingListItemCommandHandler(IMizanDbContext context, ShoppingListAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(DeleteShoppingListItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _access.LoadEditableItemAsync(request.ItemId, cancellationToken);
        var list = item.ShoppingList;
        _context.ShoppingListItems.Remove(item);
        list.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await ShoppingListAccess.InvalidateAsync(_cache, list, cancellationToken);
        return Unit.Value;
    }
}
