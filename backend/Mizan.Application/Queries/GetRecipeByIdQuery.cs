using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
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

    /// <summary>Unmeasured notes, such as "salt to taste", left out of <see cref="Nutrition"/>.</summary>
    public List<string> UnmeasuredIngredients { get; init; } = new();

    /// <summary>
    /// "calculated" when <see cref="Nutrition"/> was summed from the ingredients,
    /// "retained" when it is the value kept from the recipe's import because the
    /// ingredients cannot be summed yet. Null with no nutrition.
    /// </summary>
    public string? NutritionSource { get; init; }

    /// <summary>
    /// Measured lines without a linked food or a weight in grams. Any entry
    /// leaves <see cref="Nutrition"/> null unless a retained value still
    /// describes the recipe.
    /// </summary>
    public List<string> UnresolvedIngredients { get; init; } = new();
    public List<RecipeIngredientDto> Ingredients { get; init; } = new();
    public string? Instructions { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record RecipeIngredientDto
{
    public Guid? FoodId { get; init; }
    public string FoodName { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string IngredientText { get; init; } = string.Empty;
}

public class GetRecipeByIdQueryHandler : IRequestHandler<GetRecipeByIdQuery, RecipeDetailDto?>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public GetRecipeByIdQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<RecipeDetailDto?> Handle(GetRecipeByIdQuery request, CancellationToken cancellationToken)
    {
        // IsOwner/IsFavorited are per-viewer, and a private recipe returns null
        // for anyone but its owner - so the viewer's id has to be part of the
        // key, the same reason SearchFoodsQuery keys on it.
        var viewerId = _currentUser.UserId?.ToString() ?? "anon";

        return await _cache.GetOrCreateAsync(
            $"recipe:{request.Id}:{viewerId}",
            request,
            LoadAsync,
            CacheOptions,
            tags: [CacheTags.Recipes],
            cancellationToken: cancellationToken);
    }

    private async ValueTask<RecipeDetailDto?> LoadAsync(GetRecipeByIdQuery request, CancellationToken cancellationToken)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
                .ThenInclude(i => i.Food)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (recipe == null)
            return null;

        // Check access: must be owner or recipe must be public
        if (!recipe.IsPublic && (!_currentUser.UserId.HasValue || recipe.UserId != _currentUser.UserId))
            return null;

        // Summed from the ingredients, or a retained import value while the
        // ingredients cannot be summed and have not changed since.
        var statuses = await RecipeNutritionLookup.StatusesAsync(_context, [recipe.Id], cancellationToken);
        var status = statuses[recipe.Id];
        var totals = status.PerServing.GetValueOrDefault();

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
            IsOwner = _currentUser.UserId.HasValue && recipe.UserId == _currentUser.UserId,
            IsFavorited = _currentUser.UserId.HasValue && await _context.FavoriteRecipes.AnyAsync(f => f.UserId == _currentUser.UserId.Value && f.RecipeId == recipe.Id, cancellationToken),
            Nutrition = status.PerServing.HasValue ? new RecipeNutritionDto
            {
                CaloriesPerServing = totals.Calories,
                ProteinGrams = totals.ProteinGrams,
                CarbsGrams = totals.CarbsGrams,
                FatGrams = totals.FatGrams,
                FiberGrams = totals.FiberGrams,
                ProteinCalorieRatio = totals.ProteinCalorieRatio
            } : null,
            NutritionSource = status.Source,
            UnmeasuredIngredients = status.Unmeasured.ToList(),
            UnresolvedIngredients = status.Unresolved.ToList(),
            Ingredients = recipe.Ingredients.OrderBy(i => i.SortOrder).Select(i => new RecipeIngredientDto
            {
                FoodId = i.FoodId,
                FoodName = i.Food?.Name ?? "",
                Amount = i.Amount,
                Unit = i.Unit ?? "",
                IngredientText = i.IngredientText
            }).ToList(),
            Instructions = recipe.Instructions,
            CreatedAt = recipe.CreatedAt
        };
    }
}
