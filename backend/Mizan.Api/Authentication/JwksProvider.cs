using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Mizan.Application.Interfaces;

namespace Mizan.Api.Authentication;

public interface IJwksProvider
{
    Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default);
}

public class JwksProvider : IJwksProvider
{
    private const string MemoryCacheKey = "jwks:signing-keys";
    private const string RedisCacheKey = "jwks:raw-json";
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly IRedisCacheService _redisCache;
    private readonly ILogger<JwksProvider> _logger;
    private readonly string _jwksUrl;
    private readonly TimeSpan _memoryCacheDuration;
    private readonly TimeSpan _redisCacheDuration;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public JwksProvider(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        IRedisCacheService redisCache,
        IConfiguration configuration,
        ILogger<JwksProvider> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _redisCache = redisCache;
        _logger = logger;
        _jwksUrl = configuration["BetterAuth:JwksUrl"]
            ?? configuration["Jwt:JwksUrl"]
            ?? throw new InvalidOperationException("BetterAuth:JwksUrl is required");

        var cacheMinutes = configuration.GetValue<int?>("BetterAuth:JwksCacheMinutes")
            ?? configuration.GetValue<int?>("Jwt:JwksCacheMinutes")
            ?? 10;
        _redisCacheDuration = TimeSpan.FromMinutes(cacheMinutes);
        _memoryCacheDuration = TimeSpan.FromSeconds(30);
    }

    public async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(MemoryCacheKey, out IReadOnlyCollection<SecurityKey>? memoryCached) && memoryCached is not null)
        {
            return memoryCached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(MemoryCacheKey, out memoryCached) && memoryCached is not null)
            {
                return memoryCached;
            }

            var json = await _redisCache.GetAsync<string>(RedisCacheKey, cancellationToken);

            if (json == null)
            {
                _logger.LogInformation("JWKS cache miss (Redis + Memory), fetching from {JwksUrl}", _jwksUrl);
                json = await _httpClient.GetStringAsync(_jwksUrl, cancellationToken);
                await _redisCache.SetAsync(RedisCacheKey, json, _redisCacheDuration, cancellationToken);
            }
            else
            {
                _logger.LogDebug("JWKS loaded from Redis cache");
            }

            var jwks = new JsonWebKeySet(json);
            var keys = jwks.Keys?.Cast<SecurityKey>().ToArray() ?? Array.Empty<SecurityKey>();

            _memoryCache.Set(MemoryCacheKey, keys, _memoryCacheDuration);
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
