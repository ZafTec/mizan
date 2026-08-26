using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Contracts.Recipes;

namespace Mizan.Application.Commands;

public record CreateRecipeCommand : CreateRecipeRequest, IRequest<CreateRecipeResult>;

public record CreateRecipeResult
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
}

public class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.PrepTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PrepTimeMinutes.HasValue)
            .WithMessage("Prep time must be positive");
        RuleFor(x => x.CookTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CookTimeMinutes.HasValue)
            .WithMessage("Cook time must be positive");
        RuleFor(x => x.Ingredients).NotEmpty().WithMessage("At least one ingredient is required");
        RuleForEach(x => x.Ingredients).ChildRules(ingredient =>
        {
            ingredient.RuleFor(i => i.IngredientText).NotEmpty();
        });
    }
}

public class CreateRecipeCommandHandler : IRequestHandler<CreateRecipeCommand, CreateRecipeResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CreateRecipeCommandHandler> _logger;
    private readonly IAchievementEvaluator? _achievements;

    public CreateRecipeCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        ILogger<CreateRecipeCommandHandler> logger,
        IAchievementEvaluator? achievements = null)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
        _achievements = achievements;
    }

    public async Task<CreateRecipeResult> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            HouseholdId = request.HouseholdId,
            Title = request.Title,
            Description = request.Description,
            Instructions = request.Instructions,
            Servings = request.Servings,
            PrepTimeMinutes = request.PrepTimeMinutes,
            CookTimeMinutes = request.CookTimeMinutes,
            ImageUrl = request.ImageUrl,
            IsPublic = request.IsPublic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        for (var i = 0; i < request.Ingredients.Count; i++)
        {
            var dto = request.Ingredients[i];
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                FoodId = dto.FoodId,
                IngredientText = dto.IngredientText,
                Amount = dto.Amount,
                Unit = dto.Unit,
                SortOrder = i
            });
        }

        // No nutrition is stored. It is summed from the ingredients on read by
        // RecipeNutritionCalculator, so it cannot drift when a food changes -
        // see docs/REFOCUS.md §4. Nor is there a circular-dependency check to
        // run: reuse goes through preparations, which reference a derived Food.
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(cancellationToken);

        if (_achievements is not null)
        {
            await _achievements.EvaluateAsync(cancellationToken, ["recipes_created"]);
        }

        _logger.LogInformation("[CreateRecipe] Created {RecipeId} with {Count} ingredients",
            recipe.Id, recipe.Ingredients.Count);

        return new CreateRecipeResult { Id = recipe.Id, Title = recipe.Title };
    }
}
