using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Meals;
using Mizan.Domain.Nutrition;
using Mizan.Domain.Planning;

namespace Mizan.Application.Queries;

/// <summary>
/// POST /api/Meals/preview. The meal form's running numbers, computed here so the browser adds
/// and multiplies nothing: a food by its grams, a recipe by its servings (the per-serving totals
/// the recipe page shows), an estimate by its per-100 g figures and grams, and a custom line as
/// typed. A food or recipe the person cannot use previews as unknown. Writes nothing.
/// </summary>
public record PreviewMealQuery : MealPreviewRequest, IRequest<MealPreviewResult>;

public class PreviewMealQueryValidator : AbstractValidator<PreviewMealQuery>
{
    public const int MaxLines = 50;

    public PreviewMealQueryValidator()
    {
        RuleFor(x => x.Lines).Must(l => l.Count <= MaxLines).WithMessage($"A meal previews at most {MaxLines} lines.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Key).NotEmpty().MaximumLength(100);
            line.RuleFor(l => l.Grams).GreaterThanOrEqualTo(0).When(l => l.Grams.HasValue);
            line.RuleFor(l => l.Servings).GreaterThanOrEqualTo(0).When(l => l.Servings.HasValue);
        });
    }
}

public class PreviewMealQueryHandler : IRequestHandler<PreviewMealQuery, MealPreviewResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PreviewMealQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<MealPreviewResult> Handle(PreviewMealQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");

        var foodIds = request.Lines.Where(l => l.FoodId.HasValue).Select(l => l.FoodId!.Value).Distinct().ToList();
        var foods = foodIds.Count == 0
            ? new Dictionary<Guid, EntryNutrition>()
            : await _context.Foods.AsNoTracking()
                .Where(f => foodIds.Contains(f.Id) && (f.UserId == null || f.UserId == userId))
                .ToDictionaryAsync(
                    f => f.Id,
                    f => new EntryNutrition(f.CaloriesPer100g, f.ProteinPer100g, f.CarbsPer100g, f.FatPer100g, f.FiberPer100g ?? 0),
                    cancellationToken);

        var requestedRecipes = request.Lines.Where(l => l.RecipeId.HasValue).Select(l => l.RecipeId!.Value).Distinct().ToList();
        var usableRecipes = requestedRecipes.Count == 0
            ? []
            : await _context.Recipes.AsNoTracking()
                .Where(r => requestedRecipes.Contains(r.Id) && (r.IsPublic || r.UserId == userId))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);
        var perServing = await RecipeNutritionLookup.ForRecipesAsync(_context, usableRecipes, cancellationToken);

        var lines = request.Lines.Select(line =>
        {
            EntryNutrition? nutrition = line switch
            {
                { FoodId: { } id, Grams: { } grams } => foods.TryGetValue(id, out var per100) ? MealScale.ForGrams(per100, grams) : null,
                { RecipeId: { } id, Servings: { } servings } => perServing.TryGetValue(id, out var serving)
                    ? MealScale.ForServings(new EntryNutrition(serving.Calories, serving.ProteinGrams, serving.CarbsGrams, serving.FatGrams, serving.FiberGrams), servings)
                    : null,
                { Per100g: { } per100g, Grams: { } grams } => MealScale.ForGrams(
                    new EntryNutrition(per100g.Calories, per100g.ProteinGrams, per100g.CarbsGrams, per100g.FatGrams, per100g.FiberGrams ?? 0), grams),
                { Calories: { } calories } => new EntryNutrition(calories, line.ProteinGrams ?? 0, 0, 0, 0),
                _ => null,
            };
            return (line.Key, Nutrition: nutrition);
        }).ToList();

        var total = EntryNutrition.Sum(lines.Where(l => l.Nutrition.HasValue).Select(l => l.Nutrition!.Value));
        return new MealPreviewResult(
            lines.Select(l => new MealPreviewLineResult(l.Key, l.Nutrition?.Calories, l.Nutrition?.ProteinGrams)).ToList(),
            total.Calories,
            total.ProteinGrams,
            lines.All(l => l.Nutrition.HasValue));
    }
}
