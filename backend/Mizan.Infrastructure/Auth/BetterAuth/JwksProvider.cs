using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Mizan.Application.Common;

namespace Mizan.Infrastructure.Auth.BetterAuth;

// Fetches JWKS from the configured URL. HybridCache handles both the in-proc
// L1 and the distributed L2 (Redis) in one API and dedupes concurrent misses,
// so we don't need a SemaphoreSlim or a hand-rolled Redis+Memory cache-aside.
public sealed class JwksProvider : IJwksProvider
{
    private const string CacheKey = "jwks:signing-keys";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HybridCache _cache;
    private readonly ILogger<JwksProvider> _logger;
    private readonly JwtOptions _options;

    public JwksProvider(
        IHttpClientFactory httpClientFactory,
        HybridCache cache,
        IOptions<JwtOptions> options,
        ILogger<JwksProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.JwksUrl))
        {
            throw new InvalidOperationException("Jwt:JwksUrl is required");
        }
    }

    public async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        var json = await _cache.GetOrCreateAsync(
            CacheKey,
            FetchAsync,
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(_options.JwksCacheMinutes),
                LocalCacheExpiration = TimeSpan.FromSeconds(30)
            },
            tags: new[] { CacheTags.Jwks },
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<SecurityKey>();
        }

        var jwks = new JsonWebKeySet(json);
        return jwks.Keys?.Cast<SecurityKey>().ToArray() ?? Array.Empty<SecurityKey>();
    }

    private async ValueTask<string> FetchAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("JWKS cache miss, fetching from {JwksUrl}", _options.JwksUrl);
        var client = _httpClientFactory.CreateClient(nameof(JwksProvider));
        try
        {
            return await client.GetStringAsync(_options.JwksUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch JWKS from {JwksUrl}", _options.JwksUrl);
            return string.Empty;
        }
    }
}
