using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

/// <summary>
/// Turns a meal the user already logged into a saved recipe - see
/// docs/REFOCUS.md §4.
///
/// This is the ONLY way a recipe is authored. Ingredients and quantities come
/// from what was actually eaten, so a recipe is a byproduct of logging rather
/// than a form somebody sits down to fill in.
///
/// A meal may mix foods and recipes. Recipe entries need a derived `Food` to
/// carry their macros - see the preparations section of docs/REFOCUS.md §4 -
/// which does not exist yet, so such a meal is refused for now rather than
/// promoted with a text ingredient carrying no nutrition.
/// </summary>
public record PromoteMealToRecipeCommand(
    DateOnly EntryDate,
    string MealType,
    string Title,
    Guid? HouseholdId = null
) : IRequest<Guid>;

public class PromoteMealToRecipeCommandValidator : AbstractValidator<PromoteMealToRecipeCommand>
{
    public PromoteMealToRecipeCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MealType).NotEmpty();
    }
}

public class PromoteMealToRecipeCommandHandler : IRequestHandler<PromoteMealToRecipeCommand, Guid>
{
    /// A single food is not a recipe; it is that food. Two is the smallest
    /// combination worth naming.
    private const int MinimumEntries = 2;

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PromoteMealToRecipeCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(PromoteMealToRecipeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var userId = _currentUser.UserId.Value;

        var entries = await _context.FoodDiaryEntries
            .Where(e => e.UserId == userId
                && e.EntryDate == request.EntryDate
                && e.MealType == request.MealType)
            .OrderBy(e => e.LoggedAt)
            .ToListAsync(cancellationToken);

        if (entries.Count < MinimumEntries)
        {
            throw new DomainValidationException(
                $"A recipe needs at least {MinimumEntries} logged items; this meal has {entries.Count}");
        }

        // Until preparations land, a recipe entry has no Food to point at. Emitting
        // it as bare text would produce a recipe whose macros are quietly wrong,
        // which is the one outcome a nutrition log must not produce.
        var unresolved = entries.Where(e => e.RecipeId.HasValue).Select(e => e.Name).ToList();
        if (unresolved.Count > 0)
        {
            throw new DomainValidationException(
                "This meal contains recipes that are not yet saved as ingredients: "
                + string.Join(", ", unresolved)
                + ". Save them as preparations first so their nutrition carries over.");
        }

        var now = DateTime.UtcNow;
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            HouseholdId = request.HouseholdId,
            Title = request.Title,
            Servings = 1,
            IsPublic = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var sortOrder = 0;
        foreach (var entry in entries)
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                FoodId = entry.FoodId,
                IngredientText = entry.Name,
                Amount = entry.Servings,
                Unit = "serving",
                SortOrder = sortOrder++
            });
        }

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(cancellationToken);

        return recipe.Id;
    }
}
