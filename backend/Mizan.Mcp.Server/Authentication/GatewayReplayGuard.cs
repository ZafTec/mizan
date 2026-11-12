using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Mizan.Mcp.Server.Authentication;

/// <summary>Remembers each gateway token id until the token expires, so a token is good for one call.</summary>
public interface IGatewayReplayGuard
{
    /// <summary>True the first time <paramref name="jti"/> is seen, false after.</summary>
    Task<bool> TryClaimAsync(string jti, DateTimeOffset until, CancellationToken cancellationToken);

    internal static string Key(string jti) =>
        "mcp:gateway:jti:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(jti)));
}

/// <summary>Shared by every instance through Redis (<c>ConnectionStrings:Redis</c>).</summary>
public sealed class RedisGatewayReplayGuard : IGatewayReplayGuard
{
    private readonly Lazy<Task<ConnectionMultiplexer>> _redis;
    private readonly TimeProvider _time;

    public RedisGatewayReplayGuard(string connectionString, TimeProvider time)
    {
        _redis = new Lazy<Task<ConnectionMultiplexer>>(() => ConnectionMultiplexer.ConnectAsync(connectionString));
        _time = time;
    }

    public async Task<bool> TryClaimAsync(string jti, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var ttl = until - _time.GetUtcNow();
        if (ttl <= TimeSpan.Zero) ttl = TimeSpan.FromSeconds(1);
        var database = (await _redis.Value).GetDatabase();
        return await database.StringSetAsync(IGatewayReplayGuard.Key(jti), "1", ttl, When.NotExists);
    }
}

/// <summary>For a single instance without Redis.</summary>
public sealed class MemoryGatewayReplayGuard : IGatewayReplayGuard
{
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();

    public MemoryGatewayReplayGuard(IMemoryCache cache, TimeProvider time)
    {
        _cache = cache;
        _time = time;
    }

    public Task<bool> TryClaimAsync(string jti, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var key = IGatewayReplayGuard.Key(jti);
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out _)) return Task.FromResult(false);
            var ttl = until - _time.GetUtcNow();
            _cache.Set(key, true, ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(1));
            return Task.FromResult(true);
        }
    }
}
