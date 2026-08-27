using System.Text;
using System.Text.RegularExpressions;

namespace Mizan.Telegram.Bot;

/// <summary>
/// Turns the markdown a model writes into the small HTML subset Telegram
/// accepts.
///
/// Telegram supports b, i, u, s, code, pre, a and blockquote - and nothing
/// else. There are no lists, no headings and no tables, so those become the
/// nearest plain-text equivalent rather than being emitted as tags Telegram
/// would reject.
///
/// Escaping happens before any tag is introduced, never after. The input is
/// model output, which is untrusted: escape first and the only tags in the
/// result are the ones this class put there.
/// </summary>
public static partial class TelegramMarkdown
{
    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        var output = new StringBuilder();
        var code = new StringBuilder();
        var inCodeBlock = false;

        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCodeBlock)
                {
                    output.Append("<pre>").Append(Escape(code.ToString().TrimEnd('\n'))).Append("</pre>\n");
                    code.Clear();
                }

                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                code.Append(raw).Append('\n');
                continue;
            }

            output.Append(Line(raw)).Append('\n');
        }

        // An unterminated fence still has to come out as something.
        if (code.Length > 0)
        {
            output.Append("<pre>").Append(Escape(code.ToString().TrimEnd('\n'))).Append("</pre>");
        }

        return output.ToString().Trim();
    }

    private static string Line(string raw)
    {
        var trimmed = raw.TrimEnd();

        // A horizontal rule has no equivalent, and a stray line of dashes reads
        // like the model glitched.
        if (HorizontalRule().IsMatch(trimmed)) return string.Empty;

        var heading = Heading().Match(trimmed);
        if (heading.Success) return "<b>" + Inline(heading.Groups[1].Value) + "</b>";

        var quote = Quote().Match(trimmed);
        if (quote.Success) return "<blockquote>" + Inline(quote.Groups[1].Value) + "</blockquote>";

        // Bullets become real bullet characters, before the italic rule can
        // mistake a leading "*" for emphasis.
        var bullet = Bullet().Match(trimmed);
        if (bullet.Success)
        {
            var indent = new string(' ', bullet.Groups[1].Value.Length);
            return indent + "• " + Inline(bullet.Groups[2].Value);
        }

        return Inline(trimmed);
    }

    private static string Inline(string text)
    {
        var escaped = Escape(text);

        // Code spans come out entirely before the emphasis rules run and go
        // back in afterwards. Wrapping them in tags first is not enough: the
        // bold rule would still match the asterisks *inside* the tags, and
        // `**not bold**` would come back bold.
        var spans = new List<string>();
        escaped = InlineCode().Replace(escaped, match =>
        {
            spans.Add(match.Groups[1].Value);
            return $"\uE000{spans.Count - 1}\uE001";
        });

        escaped = Bold().Replace(escaped, "<b>$1</b>");
        escaped = BoldUnderscore().Replace(escaped, "<b>$1</b>");
        escaped = Strikethrough().Replace(escaped, "<s>$1</s>");
        escaped = Italic().Replace(escaped, "<i>$1</i>");
        escaped = ItalicUnderscore().Replace(escaped, "<i>$1</i>");

        // Links last, so a URL cannot be mangled by the emphasis rules. Only
        // http(s) survives: a "javascript:" href is never something the model
        // has a legitimate reason to produce.
        escaped = Link().Replace(escaped, match =>
        {
            var label = match.Groups[1].Value;
            var href = match.Groups[2].Value;

            return Uri.TryCreate(href, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? $"<a href=\"{href.Replace("\"", "&quot;")}\">{label}</a>"
                : label;
        });

        for (var i = 0; i < spans.Count; i++)
        {
            escaped = escaped.Replace($"\uE000{i}\uE001", $"<code>{spans[i]}</code>");
        }

        return escaped;
    }

    public static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    [GeneratedRegex(@"^\s*([-*_])\s*\1\s*\1[\s\1]*$")]
    private static partial Regex HorizontalRule();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(.+)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^\s{0,3}>\s?(.*)$")]
    private static partial Regex Quote();

    [GeneratedRegex(@"^(\s*)[-*+]\s+(.+)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"`([^`\n]+)`")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"\*\*([^*\n]+)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"__([^_\n]+)__")]
    private static partial Regex BoldUnderscore();

    [GeneratedRegex(@"~~([^~\n]+)~~")]
    private static partial Regex Strikethrough();

    [GeneratedRegex(@"(?<![\w*])\*([^*\n]+)\*(?![\w*])")]
    private static partial Regex Italic();

    [GeneratedRegex(@"(?<![\w_])_([^_\n]+)_(?![\w_])")]
    private static partial Regex ItalicUnderscore();

    /// <summary>
    /// One level of nested parentheses, so a URL like <c>alert(1)</c> is
    /// consumed whole instead of ending at the first <c>)</c> and leaving the
    /// remainder as visible debris.
    /// </summary>
    [GeneratedRegex(@"\[([^\]\n]*)\]\(([^()\s]*(?:\([^()\s]*\)[^()\s]*)*)\)")]
    private static partial Regex Link();
}
