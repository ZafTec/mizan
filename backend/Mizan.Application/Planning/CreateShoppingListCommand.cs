using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Planning;

public record CreateShoppingListCommand(string Name, Guid? HouseholdId) : IRequest<Guid>;

public class CreateShoppingListCommandValidator : AbstractValidator<CreateShoppingListCommand>
{
    public CreateShoppingListCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}

public class CreateShoppingListCommandHandler : IRequestHandler<CreateShoppingListCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly IHouseholdAccess _households;
    private readonly ShoppingListAccess _access;
    private readonly HybridCache _cache;

    public CreateShoppingListCommandHandler(IMizanDbContext context, IHouseholdAccess households, ShoppingListAccess access, HybridCache cache)
    {
        _context = context;
        _households = households;
        _access = access;
        _cache = cache;
    }

    public async Task<Guid> Handle(CreateShoppingListCommand request, CancellationToken cancellationToken)
    {
        var userId = _access.RequireUserId();

        // Without this a list could be filed under someone else's household.
        if (request.HouseholdId.HasValue && !await _households.CanAccessAsync(request.HouseholdId.Value, cancellationToken))
            throw new ForbiddenAccessException("You are not a member of this household");

        var now = DateTime.UtcNow;
        var list = new ShoppingList
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            UserId = userId,
            HouseholdId = request.HouseholdId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.ShoppingLists.Add(list);
        await _context.SaveChangesAsync(cancellationToken);
        await ShoppingListAccess.InvalidateAsync(_cache, list, cancellationToken);
        return list.Id;
    }
}
