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
