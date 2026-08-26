using System.Text.Json;

namespace Mizan.Application.Ai;

public record EvalVerdict(bool Passed, bool SchemaValid, string? Reason);

/// <summary>
/// The pure half of the eval runner: given a case's assertions and the model's
/// output, did it pass? No I/O, so the interesting part is testable without a
/// provider.
///
/// Three assertions, deliberately: substring in, substring out, and "the
/// output is JSON". Anything richer becomes a second query language nobody
/// remembers the syntax of.
/// </summary>
public static class EvalAssertions
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static EvalVerdict Evaluate(string assertionsJson, string output)
    {
        var spec = Parse(assertionsJson);
        if (spec is null)
        {
            return new EvalVerdict(false, false, "The case's assertions are not valid JSON.");
        }

        var schemaValid = !spec.RequireSchema || IsJson(output);
        if (!schemaValid)
        {
            return new EvalVerdict(false, false, "The response was not valid JSON.");
        }

        foreach (var needle in spec.MustContain ?? [])
        {
            if (!Contains(output, needle))
            {
                return new EvalVerdict(false, schemaValid, $"The response never mentioned \"{needle}\".");
            }
        }

        foreach (var needle in spec.MustNotContain ?? [])
        {
            if (Contains(output, needle))
            {
                return new EvalVerdict(false, schemaValid, $"The response mentioned \"{needle}\".");
            }
        }

        return new EvalVerdict(true, schemaValid, null);
    }

    /// <summary>Rejects a case the console would otherwise save and never be able to run.</summary>
    public static bool IsWellFormed(string assertionsJson) => Parse(assertionsJson) is not null;

    private static bool Contains(string output, string needle) =>
        !string.IsNullOrWhiteSpace(needle)
        && output.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool IsJson(string output)
    {
        try
        {
            using var _ = JsonDocument.Parse(output);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static AssertionSpec? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AssertionSpec(null, null, false);

        try
        {
            return JsonSerializer.Deserialize<AssertionSpec>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record AssertionSpec(
        List<string>? MustContain,
        List<string>? MustNotContain,
        bool RequireSchema);
}
