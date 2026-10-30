using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Planning;

/// <summary>Renames a list.</summary>
public record UpdateShoppingListCommand(Guid Id, string Name) : IRequest<Unit>;

public class UpdateShoppingListCommandValidator : AbstractValidator<UpdateShoppingListCommand>
{
    public UpdateShoppingListCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}

public class UpdateShoppingListCommandHandler : IRequestHandler<UpdateShoppingListCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ShoppingListAccess _access;
    private readonly HybridCache _cache;

    public UpdateShoppingListCommandHandler(IMizanDbContext context, ShoppingListAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(UpdateShoppingListCommand request, CancellationToken cancellationToken)
    {
        var list = await _access.LoadEditableAsync(request.Id, cancellationToken);
        list.Name = request.Name.Trim();
        list.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await ShoppingListAccess.InvalidateAsync(_cache, list, cancellationToken);
        return Unit.Value;
    }
}
