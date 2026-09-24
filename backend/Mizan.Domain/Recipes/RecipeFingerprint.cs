using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mizan.Domain.Entities;

namespace Mizan.Domain.Recipes;

/// <summary>
/// A hash of a recipe's ingredient lines: food, text, amount, and unit, in
/// order. Two recipes with the same lines hash the same; any edit to a line,
/// or adding or removing one, changes it.
///
/// The legacy nutrition import computes the same value in SQL
/// (Data/Legacy/ImportLegacyRecipeNutrition.sql). Change both or neither.
/// </summary>
public static class RecipeFingerprint
{
    public static string Of(IEnumerable<RecipeIngredient> ingredients)
    {
        var lines = ingredients
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.FoodId?.ToString() ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(i => i.IngredientText, StringComparer.Ordinal)
            .Select(Line);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)));
        return Convert.ToHexStringLower(bytes);
    }

    private static string Line(RecipeIngredient i) =>
        string.Join('|',
            i.FoodId?.ToString() ?? string.Empty,
            i.IngredientText,
            // Amounts are numeric(10,2); "0.##" matches PostgreSQL's trim_scale.
            i.Amount?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
            i.Unit?.Trim().ToLowerInvariant() ?? string.Empty);
}
