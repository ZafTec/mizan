namespace Mizan.Prices;

public class PricesOptions
{
    public const string SectionName = "Prices";

    /// <summary>Where the API lives on the internal network. Empty means the worker starts idle.</summary>
    public string? ApiUrl { get; set; }

    /// <summary>The MCP-tier service key. Internal only; never leaves the Docker network.</summary>
    public string? ServiceApiKey { get; set; }

    /// <summary>
    /// StackExchange.Redis connection string for the run lock. Empty falls back
    /// to an in-process lock, which is only safe with a single worker instance.
    /// </summary>
    public string? Redis { get; set; }

    /// <summary>Sent on every request to a scraped site, and matched against its robots.txt groups.</summary>
    public string UserAgent { get; set; } = "MizanPriceBot/1.0 (+https://mizan.zaftech.co; prices@zaftech.co)";

    /// <summary>Minimum gap between two requests to the same host. A larger robots.txt Crawl-delay wins.</summary>
    public int RequestDelayMs { get; set; } = 1000;

    /// <summary>Product pages one run may fetch. A sitemap larger than this is read in part, newest first.</summary>
    public int MaxProductsPerRun { get; set; } = 500;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiUrl)
        && !string.IsNullOrWhiteSpace(ServiceApiKey);
}
