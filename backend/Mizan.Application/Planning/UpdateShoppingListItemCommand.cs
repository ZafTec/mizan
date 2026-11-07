using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Planning;

/// <summary>Changes the fields of one item that were sent. Reviewing a generated line clears <c>NeedsReview</c>.</summary>
public record UpdateShoppingListItemCommand(Guid ItemId, UpdateShoppingListItemRequest Item) : IRequest<Unit>;

public class UpdateShoppingListItemCommandValidator : AbstractValidator<UpdateShoppingListItemCommand>
{
    public UpdateShoppingListItemCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Item).NotNull();
        RuleFor(x => x.Item.ItemName).NotEmpty().MaximumLength(255).When(x => x.Item.ItemName is not null);
        RuleFor(x => x.Item.Unit).MaximumLength(50);
        RuleFor(x => x.Item.Category).MaximumLength(100);
        RuleFor(x => x.Item.Amount).GreaterThan(0).When(x => x.Item.Amount.HasValue);
        RuleFor(x => x.Item.Grams).GreaterThan(0).When(x => x.Item.Grams.HasValue);
        RuleFor(x => x.Item.SortOrder).GreaterThanOrEqualTo(0).When(x => x.Item.SortOrder.HasValue);
    }
}

public class UpdateShoppingListItemCommandHandler : IRequestHandler<UpdateShoppingListItemCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ShoppingListAccess _access;
    private readonly HybridCache _cache;

    public UpdateShoppingListItemCommandHandler(IMizanDbContext context, ShoppingListAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(UpdateShoppingListItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _access.LoadEditableItemAsync(request.ItemId, cancellationToken);
        var change = request.Item;

        if (change.ItemName is not null) item.ItemName = change.ItemName.Trim();
        if (change.Amount.HasValue) item.Amount = change.Amount;
        if (change.Unit is not null) item.Unit = string.IsNullOrWhiteSpace(change.Unit) ? null : change.Unit.Trim();
        if (change.Grams.HasValue) item.Grams = change.Grams;
        if (change.Category is not null) item.Category = string.IsNullOrWhiteSpace(change.Category) ? null : change.Category.Trim();
        if (change.IsChecked.HasValue) item.IsChecked = change.IsChecked.Value;
        if (change.SortOrder.HasValue) item.SortOrder = change.SortOrder.Value;
        if (change.NeedsReview.HasValue) item.NeedsReview = change.NeedsReview.Value;

        item.ShoppingList.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await ShoppingListAccess.InvalidateAsync(_cache, item.ShoppingList, cancellationToken);
        return Unit.Value;
    }
}
