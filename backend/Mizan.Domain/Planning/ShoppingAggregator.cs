using Mizan.Domain.Entities;
using Mizan.Domain.Recipes;

namespace Mizan.Domain.Planning;

/// <summary>
/// One line of a shopping list built from a plan, before it is saved.
/// <see cref="Grams"/> is set when the amount is known in weight. A line that
/// could not be converted keeps its amount and unit as the recipe wrote them
/// and is marked <see cref="NeedsReview"/>.
/// </summary>
public sealed record ShoppingDraftItem(
    Guid? FoodId,
    string Name,
    decimal? Grams,
    decimal? Amount,
    string? Unit,
    bool NeedsReview,
    Guid? PrepSessionId,
    int Sources);

/// <summary>
/// Turns plan entries into shopping items. Recipe lines are scaled by the
/// entry's servings over the recipe's servings and converted to grams with the
/// recipe unit converter. Lines are grouped by food (or by normalized text when
/// no food is linked) and, when the plan has prep sessions, by session.
/// </summary>
public static class ShoppingAggregator
{
    public static IReadOnlyList<ShoppingDraftItem> Aggregate(
        IEnumerable<PlanEntry> entries,
        IReadOnlyDictionary<Guid, Recipe> recipesById,
        IReadOnlyDictionary<Guid, Food> foodsById)
    {
        var buckets = new Dictionary<(Guid? Session, string Key), Bucket>();

        foreach (var entry in entries)
        {
            switch (entry.Kind)
            {
                case PlanEntryKind.Recipe when entry.RecipeId is { } recipeId && recipesById.TryGetValue(recipeId, out var recipe):
                    {
                        var factor = (entry.Servings ?? 1m) / Math.Max(recipe.Servings, 1);
                        foreach (var line in recipe.Ingredients.OrderBy(i => i.SortOrder))
                        {
                            if (RecipeNutritionCalculator.IsUnmeasuredNote(line)) continue;
                            Food? food = line.FoodId is { } fid && foodsById.TryGetValue(fid, out var f) ? f : null;
                            var grams = food is null ? null : RecipeNutritionCalculator.Grams(line, food);
                            var name = food?.Name ?? line.IngredientText;
                            var bucket = BucketFor(buckets, entry.PrepSessionId, food?.Id, name);
                            bucket.Sources++;
                            if (grams.HasValue)
                            {
                                bucket.Grams = (bucket.Grams ?? 0m) + grams.Value * factor;
                            }
                            else
                            {
                                bucket.NeedsReview = true;
                                AddLoose(bucket, line.Amount is { } a ? a * factor : null, line.Unit);
                            }
                        }
                        break;
                    }
                case PlanEntryKind.Food when entry.FoodId is { } foodId && foodsById.TryGetValue(foodId, out var food):
                    {
                        var bucket = BucketFor(buckets, entry.PrepSessionId, food.Id, food.Name);
                        bucket.Sources++;
                        if (entry.Grams is { } grams && grams > 0) bucket.Grams = (bucket.Grams ?? 0m) + grams;
                        else bucket.NeedsReview = true;
                        break;
                    }
                default:
                    {
                        var text = string.IsNullOrWhiteSpace(entry.Text) ? "Unnamed item" : entry.Text.Trim();
                        var bucket = BucketFor(buckets, entry.PrepSessionId, null, text);
                        bucket.Sources++;
                        bucket.NeedsReview = true;
                        break;
                    }
            }
        }

        return buckets.Values
            .OrderBy(b => b.PrepSessionId.HasValue ? 0 : 1)
            .ThenBy(b => b.PrepSessionId)
            .ThenBy(b => b.NeedsReview ? 1 : 0)
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(b => new ShoppingDraftItem(
                b.FoodId,
                b.Name,
                b.Grams.HasValue ? Math.Round(b.Grams.Value, 0) : null,
                b.LooseAmount.HasValue ? Math.Round(b.LooseAmount.Value, 2) : null,
                b.LooseUnit,
                b.NeedsReview,
                b.PrepSessionId,
                b.Sources))
            .ToList();
    }

    /// <summary>Lower case, trimmed, single spaces: "Red  Onion" and "red onion" are one line.</summary>
    public static string NormalizeName(string name) =>
        string.Join(' ', name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static Bucket BucketFor(Dictionary<(Guid?, string), Bucket> buckets, Guid? session, Guid? foodId, string name)
    {
        var key = (session, foodId?.ToString() ?? "text:" + NormalizeName(name));
        if (!buckets.TryGetValue(key, out var bucket))
        {
            bucket = new Bucket { FoodId = foodId, Name = name.Trim(), PrepSessionId = session };
            buckets[key] = bucket;
        }
        return bucket;
    }

    private static void AddLoose(Bucket bucket, decimal? amount, string? unit)
    {
        var normalizedUnit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim().ToLowerInvariant();
        if (amount is null) return;
        if (bucket.LooseAmount is null || bucket.LooseUnit == normalizedUnit)
        {
            bucket.LooseAmount = (bucket.LooseAmount ?? 0m) + amount.Value;
            bucket.LooseUnit = normalizedUnit;
        }
        else
        {
            // Two different loose units for one item cannot be added; keep the first and leave it for review.
            bucket.NeedsReview = true;
        }
    }

    private sealed class Bucket
    {
        public Guid? FoodId;
        public string Name = string.Empty;
        public decimal? Grams;
        public decimal? LooseAmount;
        public string? LooseUnit;
        public bool NeedsReview;
        public Guid? PrepSessionId;
        public int Sources;
    }
}
