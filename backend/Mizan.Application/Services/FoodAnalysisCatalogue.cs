using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Nutrition;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Services;

/// <summary>
/// Ties a model's food estimate to what the person and the catalogue already know (docs/AI.md).
/// The person's own recipes come first, then their own foods and the shared catalogue, each by
/// <see cref="FoodNameMatch"/> (case, plurals and common other spellings ignored, no loose
/// contained-word matches). A recipe match carries the <c>recipeId</c>, its servings and the
/// recipe's own nutrition; a food match the <c>foodId</c> and the food's nutrition per 100 g at the
/// estimated portion. Anything else keeps the model's estimate.
/// </summary>
public sealed class FoodAnalysisCatalogue
{
    private const int CandidatesPerFood = 40;
    private const int MaxRecipes = 300;
    private readonly IMizanDbContext _context;

    public FoodAnalysisCatalogue(IMizanDbContext context) => _context = context;

    public async Task<FoodAnalysisResult> MatchAsync(Guid userId, FoodAnalysisResult result, CancellationToken cancellationToken)
    {
        if (result.Foods.Count == 0) return result;

        var recipes = await _context.Recipes.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.UpdatedAt)
            .Take(MaxRecipes)
            .Select(r => new RecipeHead(r.Id, r.Title, r.Servings, r.YieldGrams))
            .ToListAsync(cancellationToken);

        var foods = new List<RecognizedFood>(result.Foods.Count);
        var recipeHits = new List<(int Index, RecipeHead Recipe)>();
        foreach (var recognized in result.Foods)
        {
            var recipe = FoodNameMatch.Pick(recognized.Name, recipes, r => r.Title, allowContained: false);
            if (recipe is not null)
            {
                recipeHits.Add((foods.Count, recipe));
                foods.Add(recognized);
                continue;
            }

            var candidates = new Dictionary<Guid, Food>();
            foreach (var key in FoodNameMatch.SearchKeys(recognized.Name))
            {
                var found = await _context.Foods.AsNoTracking()
                    .Where(f => f.UserId == null || f.UserId == userId)
                    .Where(f => f.Name.ToLower().Contains(key))
                    .OrderByDescending(f => f.UserId == userId).ThenByDescending(f => f.IsVerified).ThenBy(f => f.Name.Length)
                    .Take(CandidatesPerFood)
                    .ToListAsync(cancellationToken);
                foreach (var food in found) candidates.TryAdd(food.Id, food);
            }
            // The person's own food first, as the query orders them, when two names tie.
            var match = FoodNameMatch.Pick(recognized.Name,
                candidates.Values.OrderByDescending(f => f.UserId == userId), f => f.Name, allowContained: false);
            foods.Add(match is null ? recognized : FromCatalogue(recognized, match));
        }

        if (recipeHits.Count > 0)
        {
            var perServing = await RecipeNutritionLookup.ForRecipesAsync(_context, recipeHits.Select(h => h.Recipe.Id).Distinct().ToList(), cancellationToken);
            foreach (var (index, recipe) in recipeHits)
            {
                // A recipe whose nutrition cannot be summed keeps the estimate, but still names the recipe.
                foods[index] = perServing.TryGetValue(recipe.Id, out var n)
                    ? FromRecipe(foods[index], recipe, n)
                    : foods[index] with { RecipeId = recipe.Id, Name = recipe.Title };
            }
        }

        return foods.Any(f => f.FoodId.HasValue || f.Source == RecognizedFood.FromRecipeSource)
            ? result with { Foods = foods, TotalCalories = Math.Round(foods.Sum(f => f.Calories), 0) }
            : result;
    }

    private sealed record RecipeHead(Guid Id, string Title, int Servings, decimal? YieldGrams);

    private static RecognizedFood FromCatalogue(RecognizedFood recognized, Food food)
    {
        var factor = recognized.PortionGrams / 100m;
        return recognized with
        {
            FoodId = food.Id,
            Name = food.Name,
            Source = RecognizedFood.FromCatalogueSource,
            Calories = Math.Round(food.CaloriesPer100g * factor, 1),
            Protein = Math.Round(food.ProteinPer100g * factor, 1),
            Carbs = Math.Round(food.CarbsPer100g * factor, 1),
            Fat = Math.Round(food.FatPer100g * factor, 1),
        };
    }

    /// <summary>
    /// The portion in servings when the recipe's finished weight is known (to a quarter, at least a
    /// quarter); otherwise one serving, at the weight the model estimated for it.
    /// </summary>
    private static RecognizedFood FromRecipe(RecognizedFood recognized, RecipeHead recipe, Domain.Recipes.RecipeNutritionTotals perServing)
    {
        var servingGrams = recipe.YieldGrams is { } yield && yield > 0 && recipe.Servings > 0 ? yield / recipe.Servings : (decimal?)null;
        var servings = servingGrams is { } grams && recognized.PortionGrams > 0
            ? Math.Max(0.25m, Math.Round(recognized.PortionGrams / grams * 4m, MidpointRounding.AwayFromZero) / 4m)
            : 1m;
        return recognized with
        {
            RecipeId = recipe.Id,
            Name = recipe.Title,
            Source = RecognizedFood.FromRecipeSource,
            Servings = servings,
            PortionGrams = servingGrams is { } g ? Math.Round(g * servings, 0) : recognized.PortionGrams,
            Calories = Math.Round(perServing.Calories * servings, 1),
            Protein = Math.Round(perServing.ProteinGrams * servings, 1),
            Carbs = Math.Round(perServing.CarbsGrams * servings, 1),
            Fat = Math.Round(perServing.FatGrams * servings, 1),
        };
    }
}
