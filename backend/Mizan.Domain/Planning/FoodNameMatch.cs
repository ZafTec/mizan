namespace Mizan.Domain.Planning;

/// <summary>
/// Picks the catalogue food a written name means. An exact name wins, ignoring case, spacing
/// and punctuation. Otherwise the closest name that contains the written one as whole words
/// ("chicken breast" finds "Chicken breast, raw"), then the closest name the written one contains
/// ("grilled chicken breast" finds "Chicken breast"). Nothing that shares less than whole words
/// with it is picked: a line that finds no food stays text for the person to review, which is
/// better than the wrong food. Words are compared in their <see cref="Canonical"/> form, so a
/// plural or a common other spelling ("eggs", "misir wat") names the same food ("Egg", "Misir wot").
/// </summary>
public static class FoodNameMatch
{
    /// <summary>A candidate shorter than this is never matched inside a longer written name.</summary>
    public const int MinimumContainedLength = 3;

    /// <summary>Lower case with single spaces: the key a written name is looked up by.</summary>
    public static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <param name="allowContained">
    /// False drops the last tier: a candidate the written name merely contains ("grilled chicken
    /// breast" finding "Chicken") is not picked. Nutrition copied from a food needs the closer match.
    /// </param>
    public static T? Pick<T>(string written, IEnumerable<T> candidates, Func<T, string> name, bool allowContained = true) where T : class
    {
        var wanted = CanonicalWords(written);
        if (wanted.Length == 0) return null;
        var wantedLength = Length(wanted);

        T? best = null;
        var bestScore = int.MaxValue;
        var bestName = string.Empty;
        foreach (var candidate in candidates)
        {
            var have = CanonicalWords(name(candidate));
            if (have.Length == 0) continue;
            var haveLength = Length(have);

            int score;
            if (have.SequenceEqual(wanted)) score = 0;
            else if (Contains(have, wanted)) score = 10_000 + (haveLength - wantedLength);
            else if (allowContained && haveLength >= MinimumContainedLength && Contains(wanted, have)) score = 20_000 + (wantedLength - haveLength);
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

    /// <summary>Other spellings of one word, each mapped to the one the catalogue uses.</summary>
    private static readonly Dictionary<string, string> Synonyms = new(StringComparer.Ordinal)
    {
        ["wat"] = "wot",
        ["wet"] = "wot",
        ["yoghurt"] = "yogurt",
        ["yogourt"] = "yogurt",
        ["garbanzo"] = "chickpea",
        ["aubergine"] = "eggplant",
        ["courgette"] = "zucchini",
        ["chilli"] = "chili",
        ["chile"] = "chili",
    };

    /// <summary>
    /// A word as names are compared: singular ("eggs" is "egg", "berries" "berry", "tomatoes"
    /// "tomato"), then the common spelling (<c>wat</c> and <c>wet</c> are <c>wot</c>). Both sides go
    /// through the same rule, so an irregular result still compares equal to itself.
    /// </summary>
    public static string Canonical(string word)
    {
        var w = word.ToLowerInvariant();
        if (w.Length > 3)
        {
            if (w.EndsWith("ies") && w.Length > 4) w = w[..^3] + "y";
            else if (w.EndsWith("oes") || w.EndsWith("ches") || w.EndsWith("shes") || w.EndsWith("xes") || w.EndsWith("sses")) w = w[..^2];
            else if (w.EndsWith('s') && !w.EndsWith("ss") && !w.EndsWith("us") && !w.EndsWith("is")) w = w[..^1];
        }
        return Synonyms.GetValueOrDefault(w, w);
    }

    /// <summary>The words of a name in their canonical form.</summary>
    public static string[] CanonicalWords(string? value) => Words(value).Select(Canonical).ToArray();

    /// <summary>
    /// What to look a written name up by in the database before <see cref="Pick"/> decides: its
    /// longest word's canonical form and the other spellings of it, each of which a matching
    /// name contains. Empty when no word is long enough to search by.
    /// </summary>
    public static IReadOnlyList<string> SearchKeys(string? written)
    {
        var longest = Words(written).OrderByDescending(w => w.Length).FirstOrDefault();
        if (longest is null || longest.Length < MinimumContainedLength) return [];
        var canonical = Canonical(longest);
        // "berry" is the stem of "berries" too once its y is dropped; a written plural searches by its singular.
        var stem = canonical.Length > 4 && canonical.EndsWith('y') ? canonical[..^1] : canonical;
        return Synonyms.Where(s => s.Value == canonical).Select(s => s.Key).Append(canonical).Prepend(stem).Distinct().ToList();
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
