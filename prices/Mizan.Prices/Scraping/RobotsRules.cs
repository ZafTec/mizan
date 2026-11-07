using System.Globalization;
using System.Text.RegularExpressions;

namespace Mizan.Prices.Scraping;

/// <summary>
/// The part of robots.txt that applies to us: the group addressed to our
/// user agent when there is one, else the <c>*</c> group. Longest matching
/// rule wins, as the REP draft and the large crawlers read it.
/// </summary>
public sealed class RobotsRules
{
    private readonly IReadOnlyList<(bool Allow, string Pattern, Regex Regex)> _rules;

    private RobotsRules(IReadOnlyList<(bool Allow, string Pattern, Regex Regex)> rules, TimeSpan? crawlDelay)
    {
        _rules = rules;
        CrawlDelay = crawlDelay;
    }

    /// <summary>What a missing or unreadable robots.txt means: everything may be fetched.</summary>
    public static RobotsRules AllowAll { get; } = new([], null);

    public TimeSpan? CrawlDelay { get; }

    public static RobotsRules Parse(string text, string userAgent)
    {
        var groups = new List<Group>();
        Group? current = null;
        var readingAgents = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine;
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            line = line.Trim();
            if (line.Length == 0) continue;

            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var field = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();

            switch (field)
            {
                case "user-agent":
                    if (current is null || !readingAgents)
                    {
                        current = new Group();
                        groups.Add(current);
                        readingAgents = true;
                    }

                    current.Agents.Add(value.ToLowerInvariant());
                    break;

                case "allow":
                case "disallow":
                    if (current is null) continue;
                    readingAgents = false;
                    if (value.Length > 0) current.Rules.Add((field == "allow", value));
                    break;

                case "crawl-delay":
                    if (current is null) continue;
                    readingAgents = false;
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
                    {
                        current.CrawlDelay = TimeSpan.FromSeconds(seconds);
                    }

                    break;

                default:
                    // Sitemap, Host and friends do not end a group, and we do not use them.
                    break;
            }
        }

        var ua = userAgent.ToLowerInvariant();
        var ours = groups.Where(g => g.Agents.Any(a => a != "*" && a.Length > 0 && ua.Contains(a))).ToList();
        var applicable = ours.Count > 0 ? ours : groups.Where(g => g.Agents.Contains("*")).ToList();

        var rules = applicable
            .SelectMany(g => g.Rules)
            .Select(r => (r.Allow, r.Pattern, ToRegex(r.Pattern)))
            .ToList();
        var delay = applicable.Select(g => g.CrawlDelay).Where(d => d.HasValue).Max();

        return new RobotsRules(rules, delay);
    }

    /// <param name="pathAndQuery">The request path including any query string, as <see cref="Uri.PathAndQuery"/> gives it.</param>
    public bool IsAllowed(string pathAndQuery)
    {
        if (string.IsNullOrEmpty(pathAndQuery)) pathAndQuery = "/";

        (bool Allow, string Pattern, Regex Regex)? winner = null;
        foreach (var rule in _rules)
        {
            if (!rule.Regex.IsMatch(pathAndQuery)) continue;
            if (winner is null || rule.Pattern.Length > winner.Value.Pattern.Length) winner = rule;
        }

        return winner?.Allow ?? true;
    }

    private static Regex ToRegex(string pattern)
    {
        var anchored = pattern.EndsWith('$');
        if (anchored) pattern = pattern[..^1];

        var body = string.Join(".*", pattern.Split('*').Select(Regex.Escape));
        return new Regex("^" + body + (anchored ? "$" : string.Empty), RegexOptions.CultureInvariant);
    }

    private sealed class Group
    {
        public List<string> Agents { get; } = [];
        public List<(bool Allow, string Pattern)> Rules { get; } = [];
        public TimeSpan? CrawlDelay { get; set; }
    }
}
