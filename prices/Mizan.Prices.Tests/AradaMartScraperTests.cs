using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mizan.Contracts.Prices;
using Mizan.Prices.Scraping;
using Mizan.Prices.Tests.Support;

namespace Mizan.Prices.Tests;

public class AradaMartScraperTests
{
    private static readonly string Sitemap = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "arada_products.xml"));
    private static readonly string ProductPage = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "arada_product.html"));

    private static readonly PriceSourceDto Source = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Arada Mart", "Scraper", "https://www.aradamart.net", AradaMartScraper.ScraperKey,
        "ET-AA", "ETB", "0 3 * * *", null);

    [Fact]
    public void TheStoreSitemap_ListsEveryProductPage_NewestFirst()
    {
        var pages = AradaMartScraper.ReadSitemap(Sitemap);

        pages.Should().HaveCount(357);
        pages.Should().OnlyContain(p => p.Host == "www.aradamart.net" && p.AbsolutePath.StartsWith("/product-page/"));
        pages.Should().OnlyHaveUniqueItems();
        pages.Should().Contain(new Uri("https://www.aradamart.net/product-page/azzy-caring-hand-wash-orange-5l"));
        // The first entry in the file is dated 2026-09-04; a later-modified page comes first.
        pages[0].Should().NotBe(new Uri("https://www.aradamart.net/product-page/azzy-caring-hand-wash-orange-5l"));
    }

    [Fact]
    public void AProductPage_YieldsThePriceFromItsJsonLd()
    {
        var observedAt = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        var observation = AradaMartScraper.ParseProductPage(ProductPage, "https://www.aradamart.net/product-page/azzy-caring-hand-wash-orange-5l", observedAt);

        observation.Should().BeEquivalentTo(new PriceObservation(
            ProductName: "Azzy Caring Hand Wash Orange 5l",
            ProductKey: "6933183203576",
            Price: 923m,
            Currency: "ETB",
            Quantity: 5m,
            Unit: "l",
            ObservedAt: observedAt,
            Url: "https://www.aradamart.net/product-page/azzy-caring-hand-wash-orange-5l"));
    }

    [Fact]
    public void WithoutJsonLd_TheOpenGraphMetasStillGiveAPrice()
    {
        var withoutJsonLd = System.Text.RegularExpressions.Regex.Replace(ProductPage, "<script.*?</script>", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        var observedAt = DateTime.UtcNow;

        var observation = AradaMartScraper.ParseProductPage(withoutJsonLd, "https://www.aradamart.net/product-page/azzy-caring-hand-wash-orange-5l", observedAt);

        observation.Should().NotBeNull();
        observation!.ProductName.Should().Be("Azzy Caring Hand Wash Orange 5l");
        observation.Price.Should().Be(923m);
        observation.Currency.Should().Be("ETB");
        observation.Quantity.Should().Be(5m);
        observation.Unit.Should().Be("l");
        observation.ProductKey.Should().Be("https://www.aradamart.net/product-page/azzy-caring-hand-wash-orange-5l");
    }

    [Fact]
    public void AnOutOfStockPage_WithAPrice_IsStillAnObservation()
    {
        var outOfStock = ProductPage.Replace("https://schema.org/InStock", "https://schema.org/OutOfStock");

        AradaMartScraper.ParseProductPage(outOfStock, "https://www.aradamart.net/x", DateTime.UtcNow)
            .Should().NotBeNull();
    }

    [Fact]
    public void APageWithoutAPrice_IsNotAnObservation()
    {
        var noPrice = ProductPage
            .Replace("\"price\":\"923\",", string.Empty)
            .Replace("<meta property=\"product:price:amount\" content=\"923\"/>", string.Empty);

        AradaMartScraper.ParseProductPage(noPrice, "https://www.aradamart.net/x", DateTime.UtcNow)
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null, "https://www.aradamart.net/store-products-sitemap.xml")]
    [InlineData("https://www.aradamart.net", "https://www.aradamart.net/store-products-sitemap.xml")]
    [InlineData("https://www.aradamart.net/shop", "https://www.aradamart.net/store-products-sitemap.xml")]
    [InlineData("https://www.aradamart.net/store-products-sitemap.xml", "https://www.aradamart.net/store-products-sitemap.xml")]
    public void TheSitemapComesFromTheSourceUrl_OrItsHost(string? sourceUrl, string expected)
    {
        AradaMartScraper.SitemapFor(sourceUrl).Should().Be(new Uri(expected));
    }

    [Fact]
    public async Task ARun_ReadsTheSitemapThenEachProductPage_AndEmitsObservations()
    {
        var pages = AradaMartScraper.ReadSitemap(Sitemap);
        var http = new FakeHttpMessageHandler()
            .Map("https://www.aradamart.net/robots.txt", "User-agent: *\nDisallow: /*?lightbox=\n", "text/plain")
            .Map("https://www.aradamart.net/store-products-sitemap.xml", Sitemap, "application/xml");
        foreach (var page in pages) http.Map(page.ToString(), ProductPage);

        var clock = new FakeClock(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));
        var ctx = new ScrapeContext(http.CreateClient(), "MizanPriceBot/1.0 (test)", TimeSpan.FromSeconds(1), maxProducts: 10, clock, NullLogger.Instance);
        var scraper = new AradaMartScraper(clock, NullLogger<AradaMartScraper>.Instance);

        var observations = await scraper.ScrapeAsync(Source, ctx, CancellationToken.None).ToListAsync();

        // The cap, not the sitemap, decides how many pages one run reads.
        observations.Should().HaveCount(10);
        observations.Should().OnlyContain(o => o.Price == 923m && o.Currency == "ETB" && o.ProductName == "Azzy Caring Hand Wash Orange 5l");
        http.RequestedUrls.Should().HaveCount(12, "robots.txt, the sitemap and ten product pages");
        http.RequestedUrls.First().Should().Be("https://www.aradamart.net/robots.txt");
        http.Requests.Should().OnlyContain(r => r.Headers.UserAgent.ToString() == "MizanPriceBot/1.0 (test)");
    }

    [Fact]
    public async Task AMissingProductPage_IsSkipped_AndTheRunGoesOn()
    {
        var pages = AradaMartScraper.ReadSitemap(Sitemap).Take(3).ToList();
        var http = new FakeHttpMessageHandler()
            .Map("https://www.aradamart.net/store-products-sitemap.xml", SitemapOf(pages), "application/xml")
            .Map(pages[0].ToString(), ProductPage)
            .Map(pages[2].ToString(), ProductPage);
        // pages[1] is unrouted and answers 404.

        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var ctx = new ScrapeContext(http.CreateClient(), "MizanPriceBot/1.0", TimeSpan.Zero, 100, clock, NullLogger.Instance);
        var scraper = new AradaMartScraper(clock, NullLogger<AradaMartScraper>.Instance);

        var observations = await scraper.ScrapeAsync(Source, ctx, CancellationToken.None).ToListAsync();

        observations.Should().HaveCount(2);
    }

    private static string SitemapOf(IEnumerable<Uri> pages) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">"
        + string.Concat(pages.Select(p => $"<url><loc>{p}</loc><lastmod>2026-10-05</lastmod></url>"))
        + "</urlset>";
}
