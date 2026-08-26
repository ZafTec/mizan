using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Contracts.Recipes;

namespace Mizan.Application.Commands;

public record UpdateRecipeCommand : UpdateRecipeRequest, IRequest<UpdateRecipeResult>;

public record UpdateRecipeResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class UpdateRecipeCommandValidator : AbstractValidator<UpdateRecipeCommand>
{
    public UpdateRecipeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.Ingredients).NotEmpty().WithMessage("At least one ingredient is required");
        RuleForEach(x => x.Ingredients).ChildRules(ingredient =>
        {
            ingredient.RuleFor(i => i.IngredientText).NotEmpty();
        });
    }
}

public class UpdateRecipeCommandHandler : IRequestHandler<UpdateRecipeCommand, UpdateRecipeResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public UpdateRecipeCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<UpdateRecipeResult> Handle(UpdateRecipeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new UpdateRecipeResult { Success = false, Message = "Unauthorized" };
        }

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (recipe == null)
        {
            return new UpdateRecipeResult { Success = false, Message = "Recipe not found" };
        }

        var user = await _context.Users.FindAsync(new object[] { _currentUser.UserId.Value }, cancellationToken);
        var isAdmin = user?.Role == "admin";

        if (recipe.UserId != _currentUser.UserId && !isAdmin)
        {
            return new UpdateRecipeResult { Success = false, Message = "You do not have permission to edit this recipe" };
        }

        recipe.Title = request.Title;
        recipe.Description = request.Description;
        recipe.Instructions = request.Instructions;
        recipe.Servings = request.Servings;
        recipe.PrepTimeMinutes = request.PrepTimeMinutes;
        recipe.CookTimeMinutes = request.CookTimeMinutes;
        recipe.ImageUrl = request.ImageUrl;
        recipe.IsPublic = request.IsPublic;
        recipe.UpdatedAt = DateTime.UtcNow;

        foreach (var ingredient in recipe.Ingredients.ToList())
        {
            _context.RecipeIngredients.Remove(ingredient);
        }
        recipe.Ingredients.Clear();

        for (var i = 0; i < request.Ingredients.Count; i++)
        {
            var dto = request.Ingredients[i];
            var ingredient = new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                FoodId = dto.FoodId,
                IngredientText = dto.IngredientText,
                Amount = dto.Amount,
                Unit = dto.Unit,
                SortOrder = i
            };
            recipe.Ingredients.Add(ingredient);
            _context.RecipeIngredients.Add(ingredient);
        }

        // Nutrition is not stored, so there is nothing here to keep in step -
        // it is summed from the ingredients on read. That is the point of
        // dropping recipe_nutrition: it could not go stale if it does not exist.
        // A preparation derived from this recipe keeps its snapshot until it is
        // re-promoted, which is deliberate; see docs/REFOCUS.md §4.
        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(CacheTags.Recipes, cancellationToken);

        return new UpdateRecipeResult { Success = true, Message = "Recipe updated successfully" };
    }
}
