using System.Net;
using Microsoft.Extensions.Logging;

namespace Mizan.Prices.Scraping;

/// <summary>
/// Every request a scraper makes goes through here, and here is where the
/// manners live: robots.txt is fetched once per host and honoured, requests to
/// one host are spaced by <see cref="PricesOptions.RequestDelayMs"/> or the
/// site's Crawl-delay, whichever is longer, each carries our User-Agent and
/// gives up after <see cref="RequestTimeout"/>, and a run fetches at most
/// <see cref="MaxProducts"/> product pages. One instance per run.
/// </summary>
public sealed class ScrapeContext
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly string _userAgent;
    private readonly TimeSpan _requestDelay;
    private readonly Dictionary<string, HostState> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private int _productsReserved;

    public ScrapeContext(
        HttpClient http,
        string userAgent,
        TimeSpan requestDelay,
        int maxProducts,
        IClock clock,
        ILogger logger)
    {
        _http = http;
        _userAgent = userAgent;
        _requestDelay = requestDelay;
        MaxProducts = maxProducts;
        _clock = clock;
        _logger = logger;
    }

    public int MaxProducts { get; }

    public int ProductsReserved => _productsReserved;

    /// <summary>
    /// Call before fetching a product page. False means the run has had its
    /// share; the rest of the catalogue waits for the next run.
    /// </summary>
    public bool TryReserveProduct()
    {
        lock (_gate)
        {
            if (_productsReserved >= MaxProducts) return false;
            _productsReserved++;
            return true;
        }
    }

    /// <summary>
    /// The body of a page, or null when robots.txt forbids the path. HTTP
    /// failures throw: a scraper decides whether one missing page ends the run.
    /// </summary>
    public async Task<string?> GetStringAsync(Uri url, CancellationToken ct)
    {
        var host = GetHost(url);

        await host.Gate.WaitAsync(ct);
        try
        {
            host.Rules ??= await FetchRobotsAsync(host, url, ct);

            if (!host.Rules.IsAllowed(url.PathAndQuery))
            {
                _logger.LogInformation("robots.txt disallows {Url}; skipped", url);
                return null;
            }

            using var response = await SendAsync(host, url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        finally
        {
            host.Gate.Release();
        }
    }

    private async Task<RobotsRules> FetchRobotsAsync(HostState host, Uri url, CancellationToken ct)
    {
        var robotsUrl = new Uri($"{url.Scheme}://{url.Authority}/robots.txt");

        try
        {
            using var response = await SendAsync(host, robotsUrl, ct);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                return RobotsRules.AllowAll;
            }

            if (!response.IsSuccessStatusCode)
            {
                // The REP says an unreachable robots.txt means "do not crawl"
                // for server errors. A site that cannot serve a text file is
                // not one we should be loading product pages from either.
                _logger.LogWarning("robots.txt at {Url} returned {Status}; treating the host as closed for this run", robotsUrl, (int)response.StatusCode);
                return RobotsRules.Parse("User-agent: *\nDisallow: /", _userAgent);
            }

            var rules = RobotsRules.Parse(await response.Content.ReadAsStringAsync(ct), _userAgent);
            if (rules.CrawlDelay is { } delay && delay > _requestDelay)
            {
                _logger.LogInformation("{Host} asks for a crawl delay of {Delay}", url.Host, delay);
            }

            return rules;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "robots.txt at {Url} could not be read; treating the host as closed for this run", robotsUrl);
            return RobotsRules.Parse("User-agent: *\nDisallow: /", _userAgent);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HostState host, Uri url, CancellationToken ct)
    {
        var delay = host.Rules?.CrawlDelay is { } crawl && crawl > _requestDelay ? crawl : _requestDelay;
        var wait = host.NextRequestAt - _clock.UtcNow;
        if (wait > TimeSpan.Zero)
        {
            await _clock.DelayAsync(wait, ct);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            host.NextRequestAt = _clock.UtcNow + delay;
            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            host.NextRequestAt = _clock.UtcNow + delay;
            throw new HttpRequestException($"{url} did not answer within {RequestTimeout.TotalSeconds:0} s");
        }
    }

    private HostState GetHost(Uri url)
    {
        lock (_gate)
        {
            if (!_hosts.TryGetValue(url.Authority, out var state))
            {
                state = new HostState();
                _hosts[url.Authority] = state;
            }

            return state;
        }
    }

    private sealed class HostState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public RobotsRules? Rules { get; set; }
        public DateTimeOffset NextRequestAt { get; set; } = DateTimeOffset.MinValue;
    }
}
