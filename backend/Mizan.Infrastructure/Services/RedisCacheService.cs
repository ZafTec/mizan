using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;
using StackExchange.Redis;

namespace Mizan.Infrastructure.Services;

public class RedisCacheService : IRedisCacheService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IDatabase? _database;
    private readonly bool _isAvailable;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer? redis = null, ILogger<RedisCacheService>? logger = null)
    {
        _redis = redis;
        _isAvailable = _redis?.IsConnected ?? false;
        _database = _isAvailable ? _redis?.GetDatabase() : null;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisCacheService>.Instance;

        if (!_isAvailable)
        {
            _logger.LogWarning("Redis cache is not available - caching operations will be skipped");
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _database == null)
            return default;

        try
        {
            _logger.LogDebug("Redis GET: {Key}", key);
            var value = await _database.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Redis cache miss: {Key}", key);
                return default;
            }

            _logger.LogDebug("Redis cache hit: {Key}", key);
            return JsonSerializer.Deserialize<T>((string)value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis GET failed for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _database == null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, expiration);
            _logger.LogDebug("Redis SET: {Key} (expires in {Expiration})", key, expiration?.ToString() ?? "never");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis SET failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _database == null)
            return;

        try
        {
            await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Redis DELETE: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis DELETE failed for key {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _redis == null)
            return;

        try
        {
            var endpoints = _redis.GetEndPoints();
            var server = _redis.GetServer(endpoints.First());
            var keys = server.Keys(pattern: $"{prefix}*").ToArray();

            if (keys.Length > 0 && _database != null)
            {
                await _database.KeyDeleteAsync(keys);
                _logger.LogInformation("Redis deleted {Count} keys with prefix {Prefix}", keys.Length, prefix);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis RemoveByPrefix failed for prefix {Prefix}", prefix);
        }
    }
}
