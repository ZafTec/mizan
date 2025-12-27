using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateRecipeCommand : IRequest<CreateRecipeResult>
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Servings { get; init; } = 1;
    public int? PrepTimeMinutes { get; init; }
    public int? CookTimeMinutes { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsPublic { get; init; }
    public Guid? HouseholdId { get; init; }
    public List<CreateRecipeIngredientDto> Ingredients { get; init; } = new();
    public List<string> Instructions { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public CreateRecipeNutritionDto? Nutrition { get; init; }
}



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

    public CreateRecipeCommandHandler(
        IMizanDbContext context, 
        ICurrentUserService currentUser,
        ILogger<CreateRecipeCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<CreateRecipeResult> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[CreateRecipe] Starting recipe creation: Title={Title}, Servings={Servings}, IngredientCount={Count}", 
            request.Title, request.Servings, request.Ingredients?.Count ?? 0);

        if (!_currentUser.UserId.HasValue)
        {
            _logger.LogWarning("[CreateRecipe] Unauthorized attempt - no user ID");
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            HouseholdId = request.HouseholdId,
            Title = request.Title,
            Description = request.Description,
            Servings = request.Servings,
            PrepTimeMinutes = request.PrepTimeMinutes,
            CookTimeMinutes = request.CookTimeMinutes,
            ImageUrl = request.ImageUrl,
            IsPublic = request.IsPublic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _logger.LogDebug("[CreateRecipe] Created recipe entity: Id={Id}, UserId={UserId}", recipe.Id, recipe.UserId);

        // Add ingredients
        if (request.Ingredients != null)
        {
            for (int i = 0; i < request.Ingredients.Count; i++)
            {
            var ingredientDto = request.Ingredients[i];
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                FoodId = ingredientDto.FoodId,
                IngredientText = ingredientDto.IngredientText,
                Amount = ingredientDto.Amount,
                Unit = ingredientDto.Unit,
                SortOrder = i
            });
            }
        }

        // Add instructions
        if (request.Instructions != null)
        {
            for (int i = 0; i < request.Instructions.Count; i++)
            {
                recipe.Instructions.Add(new RecipeInstruction
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    StepNumber = i + 1,
                    Instruction = request.Instructions[i]
                });
            }
        }

        // Add tags
        foreach (var tag in request.Tags)
        {
            recipe.Tags.Add(new RecipeTag
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                Tag = tag
            });
        }

        // Calculate nutrition from ingredients
        var ingredientFoodIds = (request.Ingredients ?? new List<CreateRecipeIngredientDto>())
            .Where(i => i.FoodId.HasValue)
            .Select(i => i.FoodId!.Value)
            .ToList();

        _logger.LogInformation("[CreateRecipe] Processing nutrition calculation. IngredientFoodIds count: {Count}", ingredientFoodIds.Count);

        if (ingredientFoodIds.Any())
        {
            var foods = await _context.Foods
                .Where(f => ingredientFoodIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, cancellationToken);

            _logger.LogInformation("[CreateRecipe] Found {FoodCount} foods in database for {RequestCount} ingredient IDs", 
                foods.Count, ingredientFoodIds.Count);

            decimal totalCalories = 0;
            decimal totalProtein = 0;
            decimal totalCarbs = 0;
            decimal totalFat = 0;
            decimal totalFiber = 0;

            foreach (var ingredientDto in request.Ingredients.Where(i => i.FoodId.HasValue && i.Amount.HasValue))
            {
                if (foods.TryGetValue(ingredientDto.FoodId!.Value, out var food))
                {
                    var ratio = ingredientDto.Amount!.Value / 100m; // Ensure decimal division
                    
                    _logger.LogDebug("[CreateRecipe] Processing ingredient: Food={FoodName}, Amount={Amount}g, Ratio={Ratio}", 
                        food.Name, ingredientDto.Amount, ratio);
                    
                    // Use decimal for calories during calculation to avoid early truncation
                    totalCalories += food.CaloriesPer100g * ratio;
                    totalProtein += food.ProteinPer100g * ratio;
                    totalCarbs += food.CarbsPer100g * ratio;
                    totalFat += food.FatPer100g * ratio;
                    totalFiber += (food.FiberPer100g ?? 0) * ratio;
                }
                else
                {
                    _logger.LogWarning("[CreateRecipe] Food not found for ingredient: FoodId={FoodId}", ingredientDto.FoodId);
                }
            }

            // Calculate per serving values
            var servings = request.Servings > 0 ? request.Servings : 1;
            
            _logger.LogInformation("[CreateRecipe] Totals BEFORE division: Calories={Cal}, Protein={Prot}g, Carbs={Carbs}g, Fat={Fat}g, Fiber={Fiber}g, Servings={Servings}", 
                totalCalories, totalProtein, totalCarbs, totalFat, totalFiber, servings);
            
            recipe.Nutrition = new RecipeNutrition
            {
                RecipeId = recipe.Id,
                CaloriesPerServing = totalCalories / servings,
                ProteinGrams = totalProtein / servings,
                CarbsGrams = totalCarbs / servings,
                FatGrams = totalFat / servings,
                FiberGrams = totalFiber / servings
            };

            _logger.LogInformation("[CreateRecipe] Final nutrition PER SERVING: Calories={Cal}, Protein={Prot}g, Carbs={Carbs}g, Fat={Fat}g, Fiber={Fiber}g", 
                recipe.Nutrition.CaloriesPerServing, recipe.Nutrition.ProteinGrams, recipe.Nutrition.CarbsGrams, 
                recipe.Nutrition.FatGrams, recipe.Nutrition.FiberGrams);
        }
        else if (request.Nutrition != null)
        {
            _logger.LogInformation("[CreateRecipe] No ingredients with FoodId, using provided nutrition data");
            // Fallback to provided nutrition only if no ingredients are linked
            recipe.Nutrition = new RecipeNutrition
            {
                RecipeId = recipe.Id,
                CaloriesPerServing = request.Nutrition.CaloriesPerServing,
                ProteinGrams = request.Nutrition.ProteinGrams,
                CarbsGrams = request.Nutrition.CarbsGrams,
                FatGrams = request.Nutrition.FatGrams,
                FiberGrams = request.Nutrition.FiberGrams
            };
        }
        else
        {
            _logger.LogWarning("[CreateRecipe] No ingredients with FoodId and no nutrition data provided");
        }

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[CreateRecipe] Recipe saved successfully: Id={Id}, Title={Title}", recipe.Id, recipe.Title);

        return new CreateRecipeResult
        {
            Id = recipe.Id,
            Title = recipe.Title
        };
    }
}
