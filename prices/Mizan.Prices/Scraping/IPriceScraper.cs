using Mizan.Contracts.Prices;

namespace Mizan.Prices.Scraping;

/// <summary>
/// One site. A scraper knows the site's layout and nothing else: politeness,
/// robots.txt and the per-run cap belong to <see cref="ScrapeContext"/>, and
/// what to do with an observation belongs to the API.
/// </summary>
public interface IPriceScraper
{
    /// <summary>Matches <c>PriceSourceDto.ScraperKey</c>. Lower case, stable, part of the admin's source form.</summary>
    string Key { get; }

    IAsyncEnumerable<PriceObservation> ScrapeAsync(PriceSourceDto source, ScrapeContext ctx, CancellationToken ct);
}
