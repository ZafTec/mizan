using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;

namespace Mizan.Application.Planning;

public record AddShoppingListItemCommand(Guid ShoppingListId, AddShoppingListItemRequest Item) : IRequest<Guid>;

public class AddShoppingListItemCommandValidator : AbstractValidator<AddShoppingListItemCommand>
{
    public AddShoppingListItemCommandValidator()
    {
        RuleFor(x => x.ShoppingListId).NotEmpty();
        RuleFor(x => x.Item).NotNull();
        RuleFor(x => x.Item.ItemName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Item.Unit).MaximumLength(50);
        RuleFor(x => x.Item.Category).MaximumLength(100);
        RuleFor(x => x.Item.Amount).GreaterThan(0).When(x => x.Item.Amount.HasValue);
        RuleFor(x => x.Item.Grams).GreaterThan(0).When(x => x.Item.Grams.HasValue);
    }
}

public class AddShoppingListItemCommandHandler : IRequestHandler<AddShoppingListItemCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ShoppingListAccess _access;
    private readonly HybridCache _cache;

    public AddShoppingListItemCommandHandler(IMizanDbContext context, ShoppingListAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Guid> Handle(AddShoppingListItemCommand request, CancellationToken cancellationToken)
    {
        var list = await _access.LoadEditableAsync(request.ShoppingListId, cancellationToken);
        var userId = _access.RequireUserId();

        if (request.Item.FoodId is { } foodId
            && !await _context.Foods.AnyAsync(f => f.Id == foodId && (f.UserId == null || f.UserId == userId), cancellationToken))
            throw new DomainValidationException("The food was not found or is not yours.");
        if (request.Item.PrepSessionId is { } sessionId
            && !await _context.PrepSessions.AnyAsync(s => s.Id == sessionId && s.MealPlanId == list.MealPlanId, cancellationToken))
            throw new DomainValidationException("The prep session does not belong to the plan this list came from.");

        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = list.Id,
            FoodId = request.Item.FoodId,
            ItemName = request.Item.ItemName.Trim(),
            Amount = request.Item.Amount,
            Unit = string.IsNullOrWhiteSpace(request.Item.Unit) ? null : request.Item.Unit.Trim(),
            Grams = request.Item.Grams,
            Category = string.IsNullOrWhiteSpace(request.Item.Category) ? null : request.Item.Category.Trim(),
            PrepSessionId = request.Item.PrepSessionId,
            SortOrder = list.Items.Select(i => i.SortOrder).DefaultIfEmpty(-1).Max() + 1,
            NeedsReview = false,
            IsChecked = false,
        };
        _context.ShoppingListItems.Add(item);
        list.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await ShoppingListAccess.InvalidateAsync(_cache, list, cancellationToken);
        return item.Id;
    }
}
