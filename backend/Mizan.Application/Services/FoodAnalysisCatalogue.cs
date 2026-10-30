using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Nutrition;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Services;

/// <summary>
/// Ties a model's food estimate to the catalogue. A recognized food whose name is a catalogue
/// food's name (or is contained in one as whole words) takes that food's id and its nutrition per
/// 100 g at the estimated portion, so "injera" carries the catalogue's protein rather than the
/// model's guess. A food with no such match keeps the estimate. Only the person's own foods and
/// the shared catalogue are candidates.
/// </summary>
public sealed class FoodAnalysisCatalogue
{
    private const int CandidatesPerFood = 40;
    private readonly IMizanDbContext _context;

    public FoodAnalysisCatalogue(IMizanDbContext context) => _context = context;

    public async Task<FoodAnalysisResult> MatchAsync(Guid userId, FoodAnalysisResult result, CancellationToken cancellationToken)
    {
        if (result.Foods.Count == 0) return result;

        var foods = new List<RecognizedFood>(result.Foods.Count);
        foreach (var recognized in result.Foods)
        {
            var words = FoodNameMatch.Words(recognized.Name);
            var key = words.OrderByDescending(w => w.Length).FirstOrDefault();
            if (key is null || key.Length < FoodNameMatch.MinimumContainedLength)
            {
                foods.Add(recognized);
                continue;
            }

            var candidates = await _context.Foods.AsNoTracking()
                .Where(f => f.UserId == null || f.UserId == userId)
                .Where(f => f.Name.ToLower().Contains(key))
                .OrderByDescending(f => f.UserId == userId).ThenByDescending(f => f.IsVerified).ThenBy(f => f.Name.Length)
                .Take(CandidatesPerFood)
                .ToListAsync(cancellationToken);
            var match = FoodNameMatch.Pick(recognized.Name, candidates, f => f.Name, allowContained: false);
            foods.Add(match is null ? recognized : FromCatalogue(recognized, match));
        }

        return foods.Any(f => f.FoodId.HasValue)
            ? result with { Foods = foods, TotalCalories = Math.Round(foods.Sum(f => f.Calories), 0) }
            : result;
    }

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
}
