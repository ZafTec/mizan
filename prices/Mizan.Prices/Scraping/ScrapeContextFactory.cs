using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mizan.Prices.Scraping;

/// <summary>A fresh context per run: robots.txt is re-read and the product cap starts at zero.</summary>
public sealed class ScrapeContextFactory
{
    public const string HttpClientName = "scraper";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<PricesOptions> _options;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;

    public ScrapeContextFactory(
        IHttpClientFactory httpClientFactory,
        IOptions<PricesOptions> options,
        IClock clock,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _clock = clock;
        _loggerFactory = loggerFactory;
    }

    public ScrapeContext Create()
    {
        var options = _options.Value;
        return new ScrapeContext(
            _httpClientFactory.CreateClient(HttpClientName),
            options.UserAgent,
            TimeSpan.FromMilliseconds(Math.Max(0, options.RequestDelayMs)),
            Math.Max(1, options.MaxProductsPerRun),
            _clock,
            _loggerFactory.CreateLogger<ScrapeContext>());
    }
}
