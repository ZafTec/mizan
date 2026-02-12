using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Mizan.Api.Authentication;

public interface IJwksProvider
{
    Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default);
}

public class JwksProvider : IJwksProvider
{
    private const string CacheKey = "jwks:signing-keys";
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<JwksProvider> _logger;
    private readonly string _jwksUrl;
    private readonly TimeSpan _cacheDuration;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public JwksProvider(
        HttpClient httpClient,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<JwksProvider> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _jwksUrl = configuration["BetterAuth:JwksUrl"]
            ?? configuration["Jwt:JwksUrl"]
            ?? throw new InvalidOperationException("BetterAuth:JwksUrl is required");

        var cacheMinutes = configuration.GetValue<int?>("BetterAuth:JwksCacheMinutes")
            ?? configuration.GetValue<int?>("Jwt:JwksCacheMinutes")
            ?? 5;
        _cacheDuration = TimeSpan.FromMinutes(cacheMinutes);
    }

    public async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyCollection<SecurityKey>? cached) && cached is not null)
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(CacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var json = await _httpClient.GetStringAsync(_jwksUrl, cancellationToken);
            var jwks = new JsonWebKeySet(json);
            var keys = jwks.Keys?.Cast<SecurityKey>().ToArray() ?? Array.Empty<SecurityKey>();

            _cache.Set(CacheKey, keys, _cacheDuration);
            return keys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch JWKS from {JwksUrl}", _jwksUrl);
            return Array.Empty<SecurityKey>();
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
