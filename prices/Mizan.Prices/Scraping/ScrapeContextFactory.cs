using Microsoft.Extensions.Logging;
using Mizan.Contracts.Prices;

namespace Mizan.Prices.Scraping;

/// <summary>A fresh context per run: robots.txt is re-read and the product cap starts at zero.</summary>
public sealed class ScrapeContextFactory
{
    public const string HttpClientName = "scraper";

    /// <summary>Used only when the API answers with no user agent at all, so no request goes out anonymous.</summary>
    public const string DefaultUserAgent = "MizanPriceBot/1.0 (+https://mizan.zaftech.co; prices@zaftech.co)";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;

    public ScrapeContextFactory(
        IHttpClientFactory httpClientFactory,
        IClock clock,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _clock = clock;
        _loggerFactory = loggerFactory;
    }

    /// <summary>A context for one run, with the crawl settings the API gave for it.</summary>
    public ScrapeContext Create(PriceCrawlSettings options)
    {
        return new ScrapeContext(
            _httpClientFactory.CreateClient(HttpClientName),
            string.IsNullOrWhiteSpace(options.UserAgent) ? DefaultUserAgent : options.UserAgent,
            TimeSpan.FromMilliseconds(Math.Max(0, options.RequestDelayMs)),
            Math.Max(1, options.MaxProductsPerRun),
            _clock,
            _loggerFactory.CreateLogger<ScrapeContext>());
    }
}
