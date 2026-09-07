using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Constants;
using Mizan.Domain.Entities;
using Mizan.Contracts.Recipes;

namespace Mizan.Application.Commands;

public record PromoteMealToRecipeCommand(
    DateOnly EntryDate,
    string MealType,
    string Title,
    Guid? HouseholdId = null,
    Dictionary<Guid, decimal>? RecipeYields = null,
    Dictionary<Guid, decimal>? EntryWeightsGrams = null
) : PromoteMealToRecipeRequest(EntryDate, MealType, Title, HouseholdId, RecipeYields, EntryWeightsGrams), IRequest<Guid>;

public class PromoteMealToRecipeCommandValidator : AbstractValidator<PromoteMealToRecipeCommand>
{
    public PromoteMealToRecipeCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MealType).Must(MealTypes.IsValid);
        RuleFor(x => x.RecipeYields).Must(x => x is null || x.Values.All(v => v > 0))
            .WithMessage("Recipe yields must be positive weights in grams");
        RuleFor(x => x.EntryWeightsGrams).Must(x => x is null || x.Values.All(v => v > 0))
            .WithMessage("Item weights must be positive weights in grams");
    }
}

public record PromotionWeightRequired(Guid Id, string Name, string Kind);
public sealed class PromotionWeightsRequiredException(IReadOnlyList<PromotionWeightRequired> items)
    : DomainValidationException("Add the finished weight in grams for these items to preserve their nutrition.")
{
    public IReadOnlyList<PromotionWeightRequired> Items { get; } = items;
}

public class PromoteMealToRecipeCommandHandler : IRequestHandler<PromoteMealToRecipeCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public PromoteMealToRecipeCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Guid> Handle(PromoteMealToRecipeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        if (request.HouseholdId.HasValue && !await _context.HouseholdMembers.AnyAsync(
                m => m.HouseholdId == request.HouseholdId && m.UserId == userId, cancellationToken))
            throw new ForbiddenAccessException("You are not a member of this household");

        var mealType = MealTypes.Normalize(request.MealType);
        var entries = await _context.FoodDiaryEntries
            .Where(e => e.UserId == userId && e.EntryDate == request.EntryDate && e.MealType.ToUpper() == mealType)
            .OrderBy(e => e.LoggedAt).ThenBy(e => e.Id).ToListAsync(cancellationToken);
        if (entries.Count < 2)
            throw new DomainValidationException($"A recipe needs at least 2 logged items; this meal has {entries.Count}");

        var foodIds = entries.Where(e => e.FoodId.HasValue).Select(e => e.FoodId!.Value).Distinct().ToList();
        var foods = await _context.Foods.Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, cancellationToken);
        var recipeIds = entries.Where(e => !e.FoodId.HasValue && e.RecipeId.HasValue)
            .Select(e => e.RecipeId!.Value).Distinct().ToList();
        var sourceRecipes = await _context.Recipes.Include(r => r.Ingredients)
            .Where(r => recipeIds.Contains(r.Id) && (r.IsPublic || r.UserId == userId))
            .ToDictionaryAsync(r => r.Id, cancellationToken);
        var gramsByEntry = new Dictionary<Guid, decimal>();
        var required = new List<PromotionWeightRequired>();
        foreach (var entry in entries)
        {
            decimal? grams = entry.AmountGrams;
            if (entry.FoodId.HasValue && foods.TryGetValue(entry.FoodId.Value, out var food))
                grams ??= entry.Servings * food.ServingSize;
            else if (entry.RecipeId.HasValue && sourceRecipes.TryGetValue(entry.RecipeId.Value, out var source))
            {
                var yield = Supplied(request.RecipeYields, source.Id) ?? source.YieldGrams;
                if (yield is > 0 && source.Servings > 0)
                    grams = yield.Value / source.Servings * entry.Servings;
                else
                    required.Add(new PromotionWeightRequired(source.Id, source.Title, "recipe"));
            }
            else
                grams ??= Supplied(request.EntryWeightsGrams, entry.Id);
            if (grams is > 0)
                gramsByEntry[entry.Id] = grams.Value;
            else if (!required.Any(r => r.Kind == "recipe" && r.Id == entry.RecipeId))
                required.Add(new PromotionWeightRequired(entry.Id, entry.Name, "entry"));
        }
        if (required.Count > 0)
            throw new PromotionWeightsRequiredException(required.DistinctBy(r => (r.Id, r.Kind)).ToList());

        var recipeId = await _context.ExecuteInTransactionAsync(async ct =>
        {
            var now = DateTime.UtcNow;
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(), UserId = userId, HouseholdId = request.HouseholdId,
                Title = request.Title.Trim(), Servings = 1, IsPublic = false,
                CreatedAt = now, UpdatedAt = now
            };
            var derived = new Dictionary<Guid, Food>();
            foreach (var entry in entries)
            {
                var grams = gramsByEntry[entry.Id];
                Food? food = entry.FoodId.HasValue ? foods.GetValueOrDefault(entry.FoodId.Value) : null;
                if (food is null && entry.RecipeId.HasValue && sourceRecipes.TryGetValue(entry.RecipeId.Value, out var source))
                {
                    if (!derived.TryGetValue(source.Id, out food))
                    {
                        food = await _context.Foods.FirstOrDefaultAsync(
                            f => f.SourceRecipeId == source.Id && f.UserId == userId, ct);
                        if (food is null || request.RecipeYields?.ContainsKey(source.Id) == true)
                        {
                            var yield = Supplied(request.RecipeYields, source.Id) ?? source.YieldGrams!.Value;
                            food = await RecipePreparation.DeriveAsync(_context, source, userId, yield, ct);
                        }
                        derived[source.Id] = food;
                    }
                }

                // The log is a snapshot. A changed catalogue food, an inherited
                // private ingredient, or a manual item receives a private copy.
                if (food is null || (food.UserId.HasValue && food.UserId != userId) || !Matches(entry, food, grams))
                {
                    food = Snapshot(entry, food, grams, userId, now);
                    _context.Foods.Add(food);
                }
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(), RecipeId = recipe.Id, FoodId = food.Id,
                    IngredientText = string.IsNullOrWhiteSpace(entry.Name) ? food.Name : entry.Name,
                    Amount = grams, Unit = "g", SortOrder = recipe.Ingredients.Count
                });
            }
            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync(ct);
            return recipe.Id;
        }, cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.Recipes, cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.Foods, cancellationToken);
        return recipeId;
    }

    private static decimal? Supplied(Dictionary<Guid, decimal>? values, Guid id) =>
        values is not null && values.TryGetValue(id, out var value) ? value : null;

    private static bool Matches(FoodDiaryEntry entry, Food food, decimal grams)
    {
        var scale = grams / 100m;
        return Equal(entry.Calories, food.CaloriesPer100g * scale)
            && Equal(entry.ProteinGrams, food.ProteinPer100g * scale)
            && Equal(entry.CarbsGrams, food.CarbsPer100g * scale)
            && Equal(entry.FatGrams, food.FatPer100g * scale)
            && Equal(entry.FiberGrams, (food.FiberPer100g ?? 0) * scale);
    }

    private static bool Equal(decimal? logged, decimal calculated) => !logged.HasValue || Math.Abs(logged.Value - calculated) <= 0.02m;

    private static Food Snapshot(FoodDiaryEntry entry, Food? original, decimal grams, Guid userId, DateTime now)
    {
        if (original is null && (!entry.Calories.HasValue || !entry.ProteinGrams.HasValue
                || !entry.CarbsGrams.HasValue || !entry.FatGrams.HasValue))
            throw new DomainValidationException($"Item '{entry.Name}' needs calories, protein, carbs and fat before it can be saved as a recipe.");
        var scale = 100m / grams;
        var calories = entry.Calories.HasValue ? Math.Round(entry.Calories.Value * scale, 2) : original!.CaloriesPer100g;
        var protein = entry.ProteinGrams.HasValue ? Math.Round(entry.ProteinGrams.Value * scale, 2) : original!.ProteinPer100g;
        return new Food
        {
            Id = Guid.NewGuid(), UserId = userId, Name = string.IsNullOrWhiteSpace(entry.Name) ? original?.Name ?? "Logged food" : entry.Name,
            ServingSize = grams, ServingUnit = "g", CaloriesPer100g = calories, ProteinPer100g = protein,
            CarbsPer100g = entry.CarbsGrams.HasValue ? Math.Round(entry.CarbsGrams.Value * scale, 2) : original!.CarbsPer100g,
            FatPer100g = entry.FatGrams.HasValue ? Math.Round(entry.FatGrams.Value * scale, 2) : original!.FatPer100g,
            FiberPer100g = entry.FiberGrams.HasValue ? Math.Round(entry.FiberGrams.Value * scale, 2) : original?.FiberPer100g,
            ProteinCalorieRatio = Food.ComputeProteinCalorieRatio(calories, protein),
            CreatedAt = now, UpdatedAt = now
        };
    }
}
