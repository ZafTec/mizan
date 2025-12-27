using System.Text.Json;
using Mizan.Application.Interfaces;
using StackExchange.Redis;

namespace Mizan.Infrastructure.Services;

public class RedisCacheService : IRedisCacheService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IDatabase? _database;
    private readonly bool _isAvailable;

    public RedisCacheService(IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
        _isAvailable = _redis?.IsConnected ?? false;
        _database = _isAvailable ? _redis?.GetDatabase() : null;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _database == null)
            return default;

        try
        {
            var value = await _database.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return default;

            return JsonSerializer.Deserialize<T>((string)value!);
        }
        catch
        {
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
        }
        catch
        {
            // Fail silently - caching is not critical
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _database == null)
            return;

        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch
        {
            // Fail silently
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
            }
        }
        catch
        {
            // Fail silently
        }
    }
}
