using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Planning;

public record ToggleShoppingListItemCommand(Guid ItemId, bool IsChecked) : IRequest<Unit>;

public class ToggleShoppingListItemCommandHandler : IRequestHandler<ToggleShoppingListItemCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ShoppingListAccess _access;
    private readonly HybridCache _cache;

    public ToggleShoppingListItemCommandHandler(IMizanDbContext context, ShoppingListAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(ToggleShoppingListItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _access.LoadEditableItemAsync(request.ItemId, cancellationToken);
        item.IsChecked = request.IsChecked;
        item.ShoppingList.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await ShoppingListAccess.InvalidateAsync(_cache, item.ShoppingList, cancellationToken);
        return Unit.Value;
    }
}
