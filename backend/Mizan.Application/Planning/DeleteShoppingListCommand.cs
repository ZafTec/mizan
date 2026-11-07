using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Planning;

public record DeleteShoppingListCommand(Guid Id) : IRequest<Unit>;

public class DeleteShoppingListCommandHandler : IRequestHandler<DeleteShoppingListCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ShoppingListAccess _access;
    private readonly HybridCache _cache;

    public DeleteShoppingListCommandHandler(IMizanDbContext context, ShoppingListAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(DeleteShoppingListCommand request, CancellationToken cancellationToken)
    {
        var list = await _access.LoadEditableAsync(request.Id, cancellationToken);
        _context.ShoppingListItems.RemoveRange(list.Items);
        _context.ShoppingLists.Remove(list);
        await _context.SaveChangesAsync(cancellationToken);
        await ShoppingListAccess.InvalidateAsync(_cache, list, cancellationToken);
        return Unit.Value;
    }
}
