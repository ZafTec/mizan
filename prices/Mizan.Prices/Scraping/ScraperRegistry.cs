namespace Mizan.Prices.Scraping;

/// <summary>
/// <c>ScraperKey</c> to scraper. Adding a site is one class and one
/// <c>AddSingleton&lt;IPriceScraper, ...&gt;</c> line in Program.cs; the
/// administrator then types its key into the source form.
/// </summary>
public sealed class ScraperRegistry
{
    private readonly Dictionary<string, IPriceScraper> _scrapers;

    public ScraperRegistry(IEnumerable<IPriceScraper> scrapers)
    {
        _scrapers = scrapers.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Keys => _scrapers.Keys.Order(StringComparer.Ordinal).ToList();

    public IPriceScraper? Find(string? key) =>
        key is not null && _scrapers.TryGetValue(key.Trim(), out var scraper) ? scraper : null;
}
