namespace Mizan.Application.Ai;

/// <summary>
/// Some providers wrap a structured reply in a ```json fence or put a sentence before it, even
/// with a response schema set. <see cref="Unwrap"/> keeps the one JSON object, from the first
/// <c>{</c> to the last <c>}</c>; the caller still deserializes it and checks its shape, and a
/// reply that fails either is a failed call (docs/AI.md). Nothing is scraped field by field.
/// </summary>
public static class StructuredReply
{
    public static string Unwrap(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        var text = content.Trim();
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}
