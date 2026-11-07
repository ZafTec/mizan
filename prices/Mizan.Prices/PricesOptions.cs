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

    // The user agent, the request delay and the product cap are runtime settings in the admin area
    // (Prices.*). The API sends them with each Run now request; the scheduler reads them before each tick.

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiUrl)
        && !string.IsNullOrWhiteSpace(ServiceApiKey);
}
