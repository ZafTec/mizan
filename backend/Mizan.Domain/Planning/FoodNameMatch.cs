namespace Mizan.Domain.Planning;

/// <summary>
/// Picks the catalogue food a written name means. An exact name wins, ignoring case, spacing
/// and punctuation. Otherwise the closest name that contains the written one as whole words
/// ("chicken breast" finds "Chicken breast, raw"), then the closest name the written one contains
/// ("grilled chicken breast" finds "Chicken breast"). Nothing that shares less than whole words
/// with it is picked: a line that finds no food stays text for the person to review, which is
/// better than the wrong food.
/// </summary>
public static class FoodNameMatch
{
    /// <summary>A candidate shorter than this is never matched inside a longer written name.</summary>
    public const int MinimumContainedLength = 3;

    /// <summary>Lower case with single spaces: the key a written name is looked up by.</summary>
    public static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static T? Pick<T>(string written, IEnumerable<T> candidates, Func<T, string> name) where T : class
    {
        var wanted = Words(written);
        if (wanted.Length == 0) return null;
        var wantedLength = Length(wanted);

        T? best = null;
        var bestScore = int.MaxValue;
        var bestName = string.Empty;
        foreach (var candidate in candidates)
        {
            var have = Words(name(candidate));
            if (have.Length == 0) continue;
            var haveLength = Length(have);

            int score;
            if (have.SequenceEqual(wanted)) score = 0;
            else if (Contains(have, wanted)) score = 10_000 + (haveLength - wantedLength);
            else if (haveLength >= MinimumContainedLength && Contains(wanted, have)) score = 20_000 + (wantedLength - haveLength);
            else continue;

            var joined = string.Join(' ', have);
            if (score < bestScore || (score == bestScore && string.CompareOrdinal(joined, bestName) < 0))
            {
                best = candidate;
                bestScore = score;
                bestName = joined;
            }
        }

        return best;
    }

    /// <summary>The words of a name: runs of letters and digits, lower case.</summary>
    public static string[] Words(string? value)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var c in (value ?? string.Empty).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
                continue;
            }
            if (current.Length > 0) words.Add(current.ToString());
            current.Clear();
        }
        if (current.Length > 0) words.Add(current.ToString());
        return words.ToArray();
    }

    private static int Length(string[] words) => words.Sum(w => w.Length) + Math.Max(0, words.Length - 1);

    /// <summary>Whether <paramref name="outer"/> holds <paramref name="inner"/> as consecutive whole words.</summary>
    private static bool Contains(string[] outer, string[] inner)
    {
        if (inner.Length > outer.Length) return false;
        for (var start = 0; start + inner.Length <= outer.Length; start++)
        {
            var all = true;
            for (var i = 0; i < inner.Length && all; i++) all = outer[start + i] == inner[i];
            if (all) return true;
        }
        return false;
    }
}
