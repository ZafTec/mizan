using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetRecipeByIdQuery(Guid Id) : IRequest<RecipeDetailDto?>;

public record RecipeDetailDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Servings { get; init; }
    public int? PrepTimeMinutes { get; init; }
    public int? CookTimeMinutes { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsPublic { get; init; }
    public bool IsOwner { get; init; }
    public bool IsFavorited { get; init; }
    public RecipeNutritionDto? Nutrition { get; init; }
    public List<RecipeIngredientDto> Ingredients { get; init; } = new();
    public List<RecipeInstructionDto> Instructions { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}

public record RecipeIngredientDto
{
    public Guid? FoodId { get; init; }
    public Guid? SubRecipeId { get; init; }
    public string FoodName { get; init; } = string.Empty;
    public string SubRecipeName { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string IngredientText { get; init; } = string.Empty;
    public RecipeNutritionDto? SubRecipeNutrition { get; init; }
}

public record RecipeInstructionDto
{
    public int StepNumber { get; init; }
    public string Instruction { get; init; } = string.Empty;
}

public class GetRecipeByIdQueryHandler : IRequestHandler<GetRecipeByIdQuery, RecipeDetailDto?>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetRecipeByIdQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<RecipeDetailDto?> Handle(GetRecipeByIdQuery request, CancellationToken cancellationToken)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Nutrition)
            .Include(r => r.Ingredients)
                .ThenInclude(i => i.Food)
            .Include(r => r.Ingredients)
                .ThenInclude(i => i.SubRecipe)
                    .ThenInclude(sr => sr.Nutrition)
            .Include(r => r.Instructions)
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (recipe == null)
            return null;

        // Check access: must be owner or recipe must be public
        if (!recipe.IsPublic && recipe.UserId != _currentUser.UserId)
            return null;

        return new RecipeDetailDto
        {
            Id = recipe.Id,
            Title = recipe.Title,
            Description = recipe.Description,
            Servings = recipe.Servings,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookTimeMinutes = recipe.CookTimeMinutes,
            ImageUrl = recipe.ImageUrl,
            IsPublic = recipe.IsPublic,
            IsOwner = recipe.UserId == _currentUser.UserId,
            IsFavorited = _currentUser.UserId.HasValue && await _context.FavoriteRecipes.AnyAsync(f => f.UserId == _currentUser.UserId.Value && f.RecipeId == recipe.Id, cancellationToken),
            Nutrition = recipe.Nutrition != null ? new RecipeNutritionDto
            {
                CaloriesPerServing = recipe.Nutrition.CaloriesPerServing,
                ProteinGrams = recipe.Nutrition.ProteinGrams,
                CarbsGrams = recipe.Nutrition.CarbsGrams,
                FatGrams = recipe.Nutrition.FatGrams
            } : null,
            Ingredients = recipe.Ingredients.Select(i => new RecipeIngredientDto
            {
                FoodId = i.FoodId,
                SubRecipeId = i.SubRecipeId,
                FoodName = i.Food?.Name ?? "",
                SubRecipeName = i.SubRecipe?.Title ?? "",
                Amount = i.Amount,
                Unit = i.Unit ?? "",
                IngredientText = i.IngredientText,
                SubRecipeNutrition = i.SubRecipe?.Nutrition != null ? new RecipeNutritionDto
                {
                    CaloriesPerServing = i.SubRecipe.Nutrition.CaloriesPerServing,
                    ProteinGrams = i.SubRecipe.Nutrition.ProteinGrams,
                    CarbsGrams = i.SubRecipe.Nutrition.CarbsGrams,
                    FatGrams = i.SubRecipe.Nutrition.FatGrams
                } : null
            }).ToList(),
            Instructions = recipe.Instructions
                .OrderBy(i => i.StepNumber)
                .Select(i => new RecipeInstructionDto
                {
                    StepNumber = i.StepNumber,
                    Instruction = i.Instruction
                }).ToList(),
            Tags = recipe.Tags.Select(t => t.Tag).ToList(),
            CreatedAt = recipe.CreatedAt
        };
    }
}
