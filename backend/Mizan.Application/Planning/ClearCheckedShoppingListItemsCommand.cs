using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Planning;

/// <summary>Removes every checked item from a list, the way a shopper clears a basket.</summary>
public record ClearCheckedShoppingListItemsCommand(Guid ShoppingListId) : IRequest<ClearCheckedResult>;

public class ClearCheckedShoppingListItemsCommandHandler : IRequestHandler<ClearCheckedShoppingListItemsCommand, ClearCheckedResult>
{
    private readonly IMizanDbContext _context;
    private readonly ShoppingListAccess _access;
    private readonly HybridCache _cache;

    public ClearCheckedShoppingListItemsCommandHandler(IMizanDbContext context, ShoppingListAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<ClearCheckedResult> Handle(ClearCheckedShoppingListItemsCommand request, CancellationToken cancellationToken)
    {
        var list = await _access.LoadEditableAsync(request.ShoppingListId, cancellationToken);
        var checkedItems = list.Items.Where(i => i.IsChecked).ToList();
        _context.ShoppingListItems.RemoveRange(checkedItems);
        list.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await ShoppingListAccess.InvalidateAsync(_cache, list, cancellationToken);
        return new ClearCheckedResult(checkedItems.Count);
    }
}
