using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Application.Validators;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record UpdateRecipeCommand : IRequest<UpdateRecipeResult>
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Servings { get; init; } = 1;
    public int? PrepTimeMinutes { get; init; }
    public int? CookTimeMinutes { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsPublic { get; init; }
    public List<CreateRecipeIngredientDto> Ingredients { get; init; } = new();
    public List<string> Instructions { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public CreateRecipeNutritionDto? Nutrition { get; init; }
}

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

                ingredient.RuleFor(i => i)
                    .Must(ing => !(ing.FoodId.HasValue && ing.SubRecipeId.HasValue))
                    .WithMessage("Each ingredient must have either FoodId or SubRecipeId, not both")
                    .Must(ing => !ing.SubRecipeId.HasValue || ing.Unit == null || ing.Unit == "serving" || ing.Unit == "servings")
                    .WithMessage("When using a recipe as an ingredient, Unit should be 'serving' or 'servings'");
            });
        }
    }

public class UpdateRecipeCommandHandler : IRequestHandler<UpdateRecipeCommand, UpdateRecipeResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateRecipeCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateRecipeResult> Handle(UpdateRecipeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new UpdateRecipeResult { Success = false, Message = "Unauthorized" };
        }

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Instructions)
            .Include(r => r.Tags)
            .Include(r => r.Nutrition)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (recipe == null)
        {
            return new UpdateRecipeResult { Success = false, Message = "Recipe not found" };
        }

        // Validate circular dependencies
        var circularDependencyValidator = new RecipeCircularDependencyValidator(_context);
        foreach (var ingredient in request.Ingredients.Where(i => i.SubRecipeId.HasValue))
        {
            if (await circularDependencyValidator.WouldCreateCircularDependency(recipe.Id, ingredient.SubRecipeId!.Value))
            {
                return new UpdateRecipeResult { Success = false, Message = "Cannot add recipe as ingredient: would create circular dependency" };
            }
        }

        var user = await _context.Users.FindAsync(new object[] { _currentUser.UserId.Value }, cancellationToken);
        var isAdmin = user?.Role == "admin";

        if (recipe.UserId != _currentUser.UserId && !isAdmin)
        {
            return new UpdateRecipeResult { Success = false, Message = "You do not have permission to edit this recipe" };
        }

        // Update properties
        recipe.Title = request.Title;
        recipe.Description = request.Description;
        recipe.Servings = request.Servings;
        recipe.PrepTimeMinutes = request.PrepTimeMinutes;
        recipe.CookTimeMinutes = request.CookTimeMinutes;
        recipe.ImageUrl = request.ImageUrl;
        recipe.IsPublic = request.IsPublic;
        recipe.UpdatedAt = DateTime.UtcNow;

        // Update Ingredients (Replace all) - Remove existing then add new
        foreach (var ingredient in recipe.Ingredients.ToList())
        {
            _context.RecipeIngredients.Remove(ingredient);
        }
        recipe.Ingredients.Clear();
        if (request.Ingredients != null)
        {
            for (int i = 0; i < request.Ingredients.Count; i++)
            {
                var ingredientDto = request.Ingredients[i];
                var newIngredient = new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    FoodId = ingredientDto.FoodId,
                    SubRecipeId = ingredientDto.SubRecipeId,
                    IngredientText = ingredientDto.IngredientText,
                    Amount = ingredientDto.Amount,
                    Unit = ingredientDto.Unit,
                    SortOrder = i
                };
                recipe.Ingredients.Add(newIngredient);
                _context.RecipeIngredients.Add(newIngredient);
            }
        }

        // Update Instructions (Replace all) - Remove existing then add new
        foreach (var instruction in recipe.Instructions.ToList())
        {
            _context.RecipeInstructions.Remove(instruction);
        }
        recipe.Instructions.Clear();
        if (request.Instructions != null)
        {
            for (int i = 0; i < request.Instructions.Count; i++)
            {
                var newInstruction = new RecipeInstruction
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    StepNumber = i + 1,
                    Instruction = request.Instructions[i]
                };
                recipe.Instructions.Add(newInstruction);
                _context.RecipeInstructions.Add(newInstruction);
            }
        }

        // Update Tags (Replace all) - Remove existing then add new
        foreach (var tag in recipe.Tags.ToList())
        {
            _context.RecipeTags.Remove(tag);
        }
        recipe.Tags.Clear();
        foreach (var tag in request.Tags)
        {
            var newTag = new RecipeTag
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                Tag = tag
            };
            recipe.Tags.Add(newTag);
            _context.RecipeTags.Add(newTag);
        }

        // Update Nutrition
        // Logic mirroring CreateRecipeCommand: Prioritize ingredients
        var ingredientFoodIds = (request.Ingredients ?? new List<CreateRecipeIngredientDto>())
            .Where(i => i.FoodId.HasValue)
            .Select(i => i.FoodId!.Value)
            .ToList();

        var subRecipeIds = (request.Ingredients ?? new List<CreateRecipeIngredientDto>())
            .Where(i => i.SubRecipeId.HasValue)
            .Select(i => i.SubRecipeId!.Value)
            .ToList();

        decimal totalCalories = 0;
        decimal totalProtein = 0;
        decimal totalCarbs = 0;
        decimal totalFat = 0;
        decimal totalFiber = 0;

        // Calculate from Food ingredients
        if (ingredientFoodIds.Any())
        {
            var foods = await _context.Foods
                .Where(f => ingredientFoodIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, cancellationToken);

            foreach (var ingredientDto in (request.Ingredients ?? []).Where(i => i.FoodId.HasValue && i.Amount.HasValue))
            {
                if (foods.TryGetValue(ingredientDto.FoodId!.Value, out var food))
                {
                    var ratio = ingredientDto.Amount!.Value / 100m;
                    totalCalories += food.CaloriesPer100g * ratio;
                    totalProtein += food.ProteinPer100g * ratio;
                    totalCarbs += food.CarbsPer100g * ratio;
                    totalFat += food.FatPer100g * ratio;
                    totalFiber += (food.FiberPer100g ?? 0) * ratio;
                }
            }
        }

        // Calculate from Sub-Recipe ingredients
        if (subRecipeIds.Any())
        {
            var subRecipes = await _context.Recipes
                .Include(r => r.Nutrition)
                .Where(r => subRecipeIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken);

            foreach (var ingredientDto in (request.Ingredients ?? []).Where(i => i.SubRecipeId.HasValue && i.Amount.HasValue))
            {
                if (subRecipes.TryGetValue(ingredientDto.SubRecipeId!.Value, out var subRecipe))
                {
                    var servings = ingredientDto.Amount!.Value;
                    if (subRecipe.Nutrition != null)
                    {
                        totalCalories += (subRecipe.Nutrition.CaloriesPerServing ?? 0) * servings;
                        totalProtein += (subRecipe.Nutrition.ProteinGrams ?? 0) * servings;
                        totalCarbs += (subRecipe.Nutrition.CarbsGrams ?? 0) * servings;
                        totalFat += (subRecipe.Nutrition.FatGrams ?? 0) * servings;
                        totalFiber += (subRecipe.Nutrition.FiberGrams ?? 0) * servings;
                    }
                }
            }
        }

        if (totalCalories > 0 || totalProtein > 0 || totalCarbs > 0 || totalFat > 0)
        {
            var servings = request.Servings > 0 ? request.Servings : 1;

            if (recipe.Nutrition == null)
            {
                recipe.Nutrition = new RecipeNutrition { RecipeId = recipe.Id };
            }

            recipe.Nutrition.CaloriesPerServing = totalCalories / servings;
            recipe.Nutrition.ProteinGrams = totalProtein / servings;
            recipe.Nutrition.CarbsGrams = totalCarbs / servings;
            recipe.Nutrition.FatGrams = totalFat / servings;
            recipe.Nutrition.FiberGrams = totalFiber / servings;
        }
        else if (request.Nutrition != null)
        {
            if (recipe.Nutrition == null)
            {
                recipe.Nutrition = new RecipeNutrition { RecipeId = recipe.Id };
            }
            recipe.Nutrition.CaloriesPerServing = request.Nutrition.CaloriesPerServing;
            recipe.Nutrition.ProteinGrams = request.Nutrition.ProteinGrams;
            recipe.Nutrition.CarbsGrams = request.Nutrition.CarbsGrams;
            recipe.Nutrition.FatGrams = request.Nutrition.FatGrams;
            recipe.Nutrition.FiberGrams = request.Nutrition.FiberGrams;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateRecipeResult { Success = true, Message = "Recipe updated successfully" };
    }
}
