using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mizan.Prices.Scraping;
using Mizan.Prices.Tests.Support;

namespace Mizan.Prices.Tests;

public class ScrapeContextTests
{
    private const string UserAgent = "MizanPriceBot/1.0 (+https://mizan.zaftech.co; prices@zaftech.co)";

    private static ScrapeContext Context(FakeHttpMessageHandler http, FakeClock clock, int delayMs = 1000, int maxProducts = 500) =>
        new(http.CreateClient(), UserAgent, TimeSpan.FromMilliseconds(delayMs), maxProducts, clock, NullLogger.Instance);

    [Fact]
    public async Task ADisallowedPath_IsNeverRequested()
    {
        var http = new FakeHttpMessageHandler()
            .Map("https://shop.example/robots.txt", "User-agent: *\nDisallow: /private/\nDisallow: /*?lightbox=\n", "text/plain")
            .Map("https://shop.example/private/secret", "nope")
            .Map("https://shop.example/product-page/x?lightbox=1", "nope")
            .Map("https://shop.example/product-page/x", "<html>ok</html>");
        var ctx = Context(http, new FakeClock(DateTimeOffset.UtcNow));

        var secret = await ctx.GetStringAsync(new Uri("https://shop.example/private/secret"), CancellationToken.None);
        var lightbox = await ctx.GetStringAsync(new Uri("https://shop.example/product-page/x?lightbox=1"), CancellationToken.None);
        var page = await ctx.GetStringAsync(new Uri("https://shop.example/product-page/x"), CancellationToken.None);

        secret.Should().BeNull();
        lightbox.Should().BeNull();
        page.Should().Be("<html>ok</html>");
        http.RequestedUrls.Should().Equal("https://shop.example/robots.txt", "https://shop.example/product-page/x");
    }

    [Fact]
    public async Task AGroupAddressedToOurBot_OverridesTheWildcardGroup()
    {
        var http = new FakeHttpMessageHandler()
            .Map("https://shop.example/robots.txt", "User-agent: *\nAllow: /\n\nUser-agent: MizanPriceBot\nDisallow: /\n", "text/plain")
            .Map("https://shop.example/anything", "nope");
        var ctx = Context(http, new FakeClock(DateTimeOffset.UtcNow));

        var body = await ctx.GetStringAsync(new Uri("https://shop.example/anything"), CancellationToken.None);

        body.Should().BeNull();
        http.RequestedUrls.Should().Equal("https://shop.example/robots.txt");
    }

    [Fact]
    public async Task RequestsToOneHost_AreSpacedByTheConfiguredDelay()
    {
        var http = new FakeHttpMessageHandler()
            .Map("https://shop.example/robots.txt", "User-agent: *\nAllow: /\n", "text/plain")
            .Map("https://shop.example/a", "a")
            .Map("https://shop.example/b", "b")
            .Map("https://other.example/robots.txt", string.Empty, "text/plain")
            .Map("https://other.example/c", "c");
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));
        var ctx = Context(http, clock, delayMs: 1000);

        await ctx.GetStringAsync(new Uri("https://shop.example/a"), CancellationToken.None);
        await ctx.GetStringAsync(new Uri("https://shop.example/b"), CancellationToken.None);
        await ctx.GetStringAsync(new Uri("https://other.example/c"), CancellationToken.None);

        // robots.txt then /a then /b on shop.example: two waits of a second.
        // other.example is a different host and its first two requests wait once.
        clock.Delays.Should().Equal(
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        http.Requests.Should().OnlyContain(r => r.Headers.UserAgent.ToString() == UserAgent);
    }

    [Fact]
    public async Task ALongerCrawlDelayInRobotsTxt_Wins()
    {
        var http = new FakeHttpMessageHandler()
            .Map("https://slow.example/robots.txt", "User-agent: *\nCrawl-delay: 5\n", "text/plain")
            .Map("https://slow.example/a", "a")
            .Map("https://slow.example/b", "b");
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var ctx = Context(http, clock, delayMs: 1000);

        await ctx.GetStringAsync(new Uri("https://slow.example/a"), CancellationToken.None);
        await ctx.GetStringAsync(new Uri("https://slow.example/b"), CancellationToken.None);

        // The wait after robots.txt is the configured one (the rules were not known when it was sent); every wait after is the site's.
        clock.Delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NoWaitIsNeeded_WhenTheDelayHasAlreadyPassed()
    {
        var http = new FakeHttpMessageHandler()
            .Map("https://shop.example/robots.txt", string.Empty, "text/plain")
            .Map("https://shop.example/a", "a")
            .Map("https://shop.example/b", "b");
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var ctx = Context(http, clock);

        await ctx.GetStringAsync(new Uri("https://shop.example/a"), CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(2));
        await ctx.GetStringAsync(new Uri("https://shop.example/b"), CancellationToken.None);

        // One wait between robots.txt and /a; none before /b, two seconds later.
        clock.Delays.Should().Equal(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AMissingRobotsTxt_AllowsEverything_AndAServerError_ClosesTheHost()
    {
        var http = new FakeHttpMessageHandler()
            .Map("https://open.example/a", "a")
            .Map("https://broken.example/robots.txt", "boom", "text/plain", System.Net.HttpStatusCode.InternalServerError)
            .Map("https://broken.example/a", "a");
        var ctx = Context(http, new FakeClock(DateTimeOffset.UtcNow));

        (await ctx.GetStringAsync(new Uri("https://open.example/a"), CancellationToken.None)).Should().Be("a");
        (await ctx.GetStringAsync(new Uri("https://broken.example/a"), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public void TheProductCap_IsPerRun()
    {
        var ctx = Context(new FakeHttpMessageHandler(), new FakeClock(DateTimeOffset.UtcNow), maxProducts: 2);

        ctx.TryReserveProduct().Should().BeTrue();
        ctx.TryReserveProduct().Should().BeTrue();
        ctx.TryReserveProduct().Should().BeFalse();
        ctx.ProductsReserved.Should().Be(2);
    }
}

public class RobotsRulesTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/product-page/teff", true)]
    [InlineData("/product-page/teff?lightbox=1", false)]
    [InlineData("/admin", false)]
    [InlineData("/admin/login", false)]
    [InlineData("/admin-public/", true)]
    [InlineData("/files/a.pdf", false)]
    [InlineData("/files/a.pdfx", true)]
    public void TheLongestMatchingRuleDecides(string path, bool allowed)
    {
        var rules = RobotsRules.Parse(
            """
            # Wix-style file
            User-agent: *
            Disallow: /*?lightbox=
            Disallow: /admin
            Allow: /admin-public/
            Disallow: /*.pdf$
            Sitemap: https://shop.example/sitemap.xml
            """, "MizanPriceBot/1.0");

        rules.IsAllowed(path).Should().Be(allowed);
    }

    [Fact]
    public void AnEmptyFile_AllowsEverything()
    {
        RobotsRules.Parse(string.Empty, "MizanPriceBot/1.0").IsAllowed("/anything").Should().BeTrue();
    }
}
