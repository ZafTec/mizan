using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Mizan.Contracts.Prices;

namespace Mizan.Prices.Scraping;

/// <summary>
/// aradamart.net: a Wix store in Addis Ababa, prices in ETB. Wix publishes a
/// store sitemap with every product URL and renders a schema.org Product on
/// each page server-side, so no browser is needed. Probed 2026-10-22.
/// </summary>
public sealed partial class AradaMartScraper : IPriceScraper
{
    public const string ScraperKey = "aradamart";

    private const string DefaultSitemap = "https://www.aradamart.net/store-products-sitemap.xml";

    private static readonly XNamespace Sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private readonly IClock _clock;
    private readonly ILogger<AradaMartScraper> _logger;

    public AradaMartScraper(IClock clock, ILogger<AradaMartScraper> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public string Key => ScraperKey;

    public async IAsyncEnumerable<PriceObservation> ScrapeAsync(
        PriceSourceDto source,
        ScrapeContext ctx,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var sitemapUrl = SitemapFor(source.Url);
        var xml = await ctx.GetStringAsync(sitemapUrl, ct)
            ?? throw new InvalidOperationException($"robots.txt forbids the sitemap at {sitemapUrl}");

        var pages = ReadSitemap(xml);
        _logger.LogInformation("{Source}: sitemap lists {Count} product pages", source.Name, pages.Count);

        var missing = 0;
        foreach (var page in pages)
        {
            if (!ctx.TryReserveProduct())
            {
                _logger.LogInformation("{Source}: product cap of {Cap} reached; the rest waits for the next run", source.Name, ctx.MaxProducts);
                break;
            }

            string? html;
            try
            {
                html = await ctx.GetStringAsync(page, ct);
            }
            catch (HttpRequestException ex)
            {
                // One page gone is a stale sitemap entry, not a broken site.
                // A site that is down fails every page, and that ends the run.
                _logger.LogWarning("{Url}: {Error}", page, ex.Message);
                if (++missing >= 5) throw new InvalidOperationException($"{missing} product pages failed in a row; stopping", ex);
                continue;
            }

            if (html is null) continue;
            missing = 0;

            var observation = ParseProductPage(html, page.ToString(), _clock.UtcNow.UtcDateTime);
            if (observation is null)
            {
                _logger.LogDebug("{Url}: no price on the page", page);
                continue;
            }

            yield return observation;
        }
    }

    /// <summary>The source URL when it is already the sitemap, else the store sitemap on that host.</summary>
    public static Uri SitemapFor(string? sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl)) return new Uri(DefaultSitemap);

        var url = new Uri(sourceUrl, UriKind.Absolute);
        return url.AbsolutePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            ? url
            : new Uri($"{url.Scheme}://{url.Authority}/store-products-sitemap.xml");
    }

    /// <summary>Product page URLs from a Wix store sitemap, most recently modified first so a capped run reads the fresh pages.</summary>
    public static IReadOnlyList<Uri> ReadSitemap(string xml)
    {
        var document = XDocument.Parse(xml);

        return document.Root?
            .Elements(Sitemap + "url")
            .Select(url => (
                Loc: url.Element(Sitemap + "loc")?.Value.Trim(),
                LastMod: url.Element(Sitemap + "lastmod")?.Value.Trim()))
            .Where(entry => entry.Loc is not null && entry.Loc.Contains("/product-page/", StringComparison.Ordinal))
            .OrderByDescending(entry => entry.LastMod ?? string.Empty, StringComparer.Ordinal)
            .Select(entry => new Uri(entry.Loc!, UriKind.Absolute))
            .ToList() ?? [];
    }

    /// <summary>
    /// The JSON-LD Product first, the Open Graph product metas second. Out of
    /// stock pages still carry their last price, and that price is still the
    /// shelf price, so they are observations too. No price, no observation.
    /// </summary>
    public static PriceObservation? ParseProductPage(string html, string url, DateTime observedAtUtc)
    {
        return FromJsonLd(html, url, observedAtUtc) ?? FromMetaTags(html, url, observedAtUtc);
    }

    private static PriceObservation? FromJsonLd(string html, string url, DateTime observedAtUtc)
    {
        foreach (Match script in JsonLdScript().Matches(html))
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(WebUtility.HtmlDecode(script.Groups["json"].Value));
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                foreach (var product in Products(document.RootElement))
                {
                    var name = Text(product, "name");
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    foreach (var offer in Offers(product))
                    {
                        var price = ParsePrice(Text(offer, "price") ?? Text(offer, "lowPrice"));
                        if (price is null) continue;

                        var currency = Text(offer, "priceCurrency") ?? "ETB";
                        var sku = Text(product, "sku");
                        var offerUrl = Text(offer, "url") ?? url;
                        var quantity = QuantityParser.Parse(name);

                        return new PriceObservation(
                            ProductName: name.Trim(),
                            ProductKey: string.IsNullOrWhiteSpace(sku) ? offerUrl : sku,
                            Price: price.Value,
                            Currency: currency.ToUpperInvariant(),
                            Quantity: quantity?.Quantity,
                            Unit: quantity?.Unit,
                            ObservedAt: observedAtUtc,
                            Url: offerUrl);
                    }
                }
            }
        }

        return null;
    }

    private static PriceObservation? FromMetaTags(string html, string url, DateTime observedAtUtc)
    {
        var price = ParsePrice(Meta(html, "product:price:amount"));
        if (price is null) return null;

        var title = Meta(html, "og:title") ?? TitleTag().Match(html).Groups["title"].Value;
        var name = CleanTitle(WebUtility.HtmlDecode(title));
        if (string.IsNullOrWhiteSpace(name)) return null;

        var pageUrl = Meta(html, "og:url") ?? url;
        var quantity = QuantityParser.Parse(name);

        return new PriceObservation(
            ProductName: name,
            ProductKey: pageUrl,
            Price: price.Value,
            Currency: (Meta(html, "product:price:currency") ?? "ETB").ToUpperInvariant(),
            Quantity: quantity?.Quantity,
            Unit: quantity?.Unit,
            ObservedAt: observedAtUtc,
            Url: pageUrl);
    }

    /// <summary>Wix titles read "Buy Azzy Hand Wash 5l | Arada Mart Addis Ababa" and the og:title appends the description.</summary>
    private static string CleanTitle(string title)
    {
        var name = title.Trim();
        var bar = name.IndexOf(" | ", StringComparison.Ordinal);
        if (bar > 0) name = name[..bar];
        if (name.StartsWith("Buy ", StringComparison.OrdinalIgnoreCase)) name = name[4..];
        return name.Trim();
    }

    private static IEnumerable<JsonElement> Products(JsonElement root)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                {
                    foreach (var product in Products(item)) yield return product;
                }

                break;

            case JsonValueKind.Object:
                if (IsType(root, "Product")) yield return root;

                if (root.TryGetProperty("@graph", out var graph))
                {
                    foreach (var product in Products(graph)) yield return product;
                }

                break;
        }
    }

    private static IEnumerable<JsonElement> Offers(JsonElement product)
    {
        if (!product.TryGetProperty("offers", out var offers)) yield break;

        if (offers.ValueKind == JsonValueKind.Object)
        {
            yield return offers;
        }
        else if (offers.ValueKind == JsonValueKind.Array)
        {
            foreach (var offer in offers.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.Object)) yield return offer;
        }
    }

    private static bool IsType(JsonElement element, string type)
    {
        if (!element.TryGetProperty("@type", out var typeElement)) return false;

        return typeElement.ValueKind switch
        {
            JsonValueKind.String => string.Equals(typeElement.GetString(), type, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => typeElement.EnumerateArray().Any(t => string.Equals(t.GetString(), type, StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static decimal? ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var cleaned = text.Replace(",", string.Empty).Replace("ETB", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price) && price > 0
            ? price
            : null;
    }

    private static string? Meta(string html, string property)
    {
        var match = MetaTag().Matches(html)
            .FirstOrDefault(m => string.Equals(m.Groups["property"].Value, property, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : WebUtility.HtmlDecode(match.Groups["content"].Value);
    }

    [GeneratedRegex(@"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(?<json>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex JsonLdScript();

    [GeneratedRegex(@"<meta\s+(?=[^>]*property\s*=\s*(?<pq>[""'])(?<property>.*?)\k<pq>)(?=[^>]*content\s*=\s*(?<cq>[""'])(?<content>.*?)\k<cq>)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaTag();

    [GeneratedRegex(@"<title[^>]*>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TitleTag();
}
