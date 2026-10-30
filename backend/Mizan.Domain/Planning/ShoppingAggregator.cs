using Mizan.Domain.Entities;
using Mizan.Domain.Recipes;

namespace Mizan.Domain.Planning;

/// <summary>
/// One line of a shopping list built from a plan, before it is saved.
/// <see cref="Grams"/> is set when the amount is known in weight. A line that
/// could not be converted keeps its amount and unit as the recipe wrote them
/// and is marked <see cref="NeedsReview"/>. <see cref="PrepSessionId"/> is set
/// only when every use of the item belongs to that one prep session.
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
/// recipe unit converter. Each item is one line for the whole plan: lines are
/// merged by food, and a line without a linked food joins the food of the same
/// name (or other lines of the same normalized text). Amounts that cannot be
/// weighed are added when their units agree or convert (spoons, cups, litres);
/// otherwise the first amount is kept and the line needs review.
/// </summary>
public static class ShoppingAggregator
{
    public static IReadOnlyList<ShoppingDraftItem> Aggregate(
        IEnumerable<PlanEntry> entries,
        IReadOnlyDictionary<Guid, Recipe> recipesById,
        IReadOnlyDictionary<Guid, Food> foodsById)
    {
        var buckets = new Dictionary<string, Bucket>();

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
                            var bucket = BucketFor(buckets, food?.Id, food?.Name ?? line.IngredientText);
                            bucket.Use(entry.PrepSessionId);
                            if (grams.HasValue)
                            {
                                bucket.Grams = (bucket.Grams ?? 0m) + grams.Value * factor;
                            }
                            else
                            {
                                bucket.NeedsReview = true;
                                bucket.AddLoose(line.Amount is { } a ? a * factor : null, line.Unit);
                            }
                        }
                        break;
                    }
                case PlanEntryKind.Food when entry.FoodId is { } foodId && foodsById.TryGetValue(foodId, out var food):
                    {
                        var bucket = BucketFor(buckets, food.Id, food.Name);
                        bucket.Use(entry.PrepSessionId);
                        if (entry.Grams is { } grams && grams > 0) bucket.Grams = (bucket.Grams ?? 0m) + grams;
                        else bucket.NeedsReview = true;
                        break;
                    }
                default:
                    {
                        var text = string.IsNullOrWhiteSpace(entry.Text) ? "Unnamed item" : entry.Text.Trim();
                        var bucket = BucketFor(buckets, null, text);
                        bucket.Use(entry.PrepSessionId);
                        bucket.NeedsReview = true;
                        break;
                    }
            }
        }

        // A typed line named like one food on the list is that food: "berbere" joins Berbere.
        var byFoodName = buckets.Values.Where(b => b.FoodId.HasValue)
            .GroupBy(b => NormalizeName(b.Name))
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single());
        foreach (var (key, text) in buckets.Where(kv => kv.Value.FoodId is null).ToList())
        {
            if (!byFoodName.TryGetValue(NormalizeName(text.Name), out var food)) continue;
            food.Absorb(text);
            buckets.Remove(key);
        }

        return buckets.Values
            .Select(b => new ShoppingDraftItem(
                b.FoodId,
                b.Name,
                b.Grams.HasValue ? Math.Round(b.Grams.Value, 0) : null,
                b.LooseAmount.HasValue ? Math.Round(b.LooseAmount.Value, 2) : null,
                b.LooseUnit,
                b.NeedsReview,
                b.Session,
                b.Sources))
            .OrderBy(i => i.PrepSessionId.HasValue ? 0 : 1)
            .ThenBy(i => i.PrepSessionId)
            .ThenBy(i => i.NeedsReview ? 1 : 0)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Lower case, trimmed, single spaces: "Red  Onion" and "red onion" are one line.</summary>
    public static string NormalizeName(string name) =>
        string.Join(' ', name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// The amount written in <paramref name="from"/> expressed in <paramref name="to"/>, or null when
    /// the two cannot be compared (a handful and a cup). Spellings of one unit agree ("tbsp",
    /// "tablespoons"); spoons, cups and litres convert by volume. No unit counts pieces.
    /// </summary>
    public static decimal? ConvertLoose(decimal amount, string? from, string? to)
    {
        var (fromUnit, fromMl) = Canonical(from);
        var (toUnit, toMl) = Canonical(to);
        if (fromUnit == toUnit) return amount;
        if (fromMl is { } a && toMl is { } b) return amount * a / b;
        return null;
    }

    private static (string Unit, decimal? Millilitres) Canonical(string? unit) =>
        (string.IsNullOrWhiteSpace(unit) ? "" : unit.Trim().ToLowerInvariant().TrimEnd('.')) switch
        {
            "" or "piece" or "pieces" or "pc" or "pcs" or "whole" => ("piece", null),
            "tsp" or "teaspoon" or "teaspoons" => ("tsp", 5m),
            "tbsp" or "tablespoon" or "tablespoons" => ("tbsp", 15m),
            "cup" or "cups" => ("cup", 240m),
            "ml" or "millilitre" or "millilitres" or "milliliter" or "milliliters" => ("ml", 1m),
            "l" or "litre" or "litres" or "liter" or "liters" => ("l", 1000m),
            "clove" or "cloves" => ("clove", null),
            "bunch" or "bunches" => ("bunch", null),
            var other => (other, null),
        };

    private static Bucket BucketFor(Dictionary<string, Bucket> buckets, Guid? foodId, string name)
    {
        var key = foodId?.ToString() ?? "text:" + NormalizeName(name);
        if (!buckets.TryGetValue(key, out var bucket))
        {
            bucket = new Bucket { FoodId = foodId, Name = name.Trim() };
            buckets[key] = bucket;
        }
        return bucket;
    }

    private sealed class Bucket
    {
        public Guid? FoodId;
        public string Name = string.Empty;
        public decimal? Grams;
        public decimal? LooseAmount;
        public string? LooseUnit;
        public bool NeedsReview;
        public int Sources;
        private readonly HashSet<Guid?> _sessions = [];

        /// <summary>The one prep session every use belongs to; null when uses span sessions or have none.</summary>
        public Guid? Session => _sessions.Count == 1 ? _sessions.Single() : null;

        public void Use(Guid? session)
        {
            Sources++;
            _sessions.Add(session);
        }

        public void AddLoose(decimal? amount, string? unit)
        {
            if (amount is null) return;
            var written = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim().ToLowerInvariant();
            if (LooseAmount is null)
            {
                LooseAmount = amount;
                LooseUnit = written;
                return;
            }
            if (ConvertLoose(amount.Value, written, LooseUnit) is { } converted)
                LooseAmount += converted;
            else
                NeedsReview = true; // Two amounts that cannot be added: keep the first for review.
        }

        public void Absorb(Bucket other)
        {
            if (other.Grams.HasValue) Grams = (Grams ?? 0m) + other.Grams.Value;
            if (other.LooseAmount.HasValue) AddLoose(other.LooseAmount, other.LooseUnit);
            NeedsReview |= other.NeedsReview;
            Sources += other.Sources;
            foreach (var session in other._sessions) _sessions.Add(session);
        }
    }
}
