using System.Text.RegularExpressions;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// The line a person sees under an answer for one tool call. The model reads the whole result;
/// the person sees its first statement, without ids, or the tool's name when the result is a list.
/// </summary>
public static partial class AiActionLabel
{
    private const int MaxLength = 100;

    public static string For(string tool, string text)
    {
        var line = text.Split('\n', 2)[0].Trim();
        // A list starts with an item's id; its first row is not what was done.
        if (StartsWithId().IsMatch(line)) return Name(tool);
        line = IdInParentheses().Replace(line, string.Empty);
        line = Id().Replace(line, string.Empty).Trim();

        var colon = line.IndexOf(": ", StringComparison.Ordinal);
        if (colon > 0) line = line[..colon];

        var stop = line.IndexOf(". ", StringComparison.Ordinal);
        if (stop > 0) line = line[..(stop + 1)];

        line = line.Trim();
        if (line.Length == 0 || line.Length > MaxLength || !char.IsLetterOrDigit(line[0]) && line[0] != '"')
            return Name(tool);
        return line;
    }

    private static string Name(string tool)
    {
        var words = tool.Replace('_', ' ').Trim();
        return words.Length == 0 ? "Done" : char.ToUpperInvariant(words[0]) + words[1..];
    }

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-")]
    private static partial Regex StartsWithId();

    [GeneratedRegex(@"\s*\([^()]*[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}[^()]*\)")]
    private static partial Regex IdInParentheses();

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\s*[—-]?\s*")]
    private static partial Regex Id();
}
