using System.Globalization;

namespace Mizan.Mcp.Server.Tools;

/// <summary>
/// Parsing at the tool boundary, with messages an agent can act on.
///
/// Since the spine's writes moved onto Mizan.Contracts these conversions happen
/// here rather than in the API, so the error has to name the argument the
/// caller got wrong - "Invalid foodId" beats "Unrecognized Guid format."
/// </summary>
internal static class ToolArguments
{
    public static Guid ParseId(string value, string field)
    {
        if (!Guid.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Invalid {field} '{value}'. Expected a Guid, e.g. 3f2504e0-4f89-11d3-9a0c-0305e82c3301.");
        }
        return parsed;
    }

    public static Guid? ParseOptionalId(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseId(value, field);

    public static DateOnly ParseDate(string value, string field)
    {
        if (!DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"Invalid {field} '{value}'. Expected YYYY-MM-DD.");
        }
        return parsed;
    }

    public static DateOnly? ParseOptionalDate(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDate(value, field);

    public static DateTime? ParseOptionalTimestamp(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            throw new ArgumentException($"Invalid {field} '{value}'. Expected ISO 8601, e.g. 2026-04-20T16:14:54Z.");
        }
        return parsed.ToUniversalTime();
    }
}

/// <summary>
/// Builds a query string that skips what was not supplied.
///
/// The alternative is interpolation with a pile of ternaries, which is where
/// `?page=1&amp;search=` and double ampersands come from - and where an
/// unescaped value gets through.
/// </summary>
public sealed class QueryString
{
    private readonly List<string> _parts = [];

    public QueryString Add(string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _parts.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }

        return this;
    }

    public QueryString Add(string name, int value) => Add(name, value.ToString());

    public override string ToString() => _parts.Count == 0 ? string.Empty : "?" + string.Join("&", _parts);
}
