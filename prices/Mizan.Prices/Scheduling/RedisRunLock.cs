using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mizan.Prices.Scheduling;

/// <summary>
/// SET NX PX, released by a compare-and-delete script so a worker that ran
/// past the TTL cannot delete the lease a second worker took in the meantime.
/// The connection is opened lazily: a Redis that is down at boot delays the
/// first run, not the process.
/// </summary>
public sealed class RedisRunLock : IRunLock, IAsyncDisposable
{
    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private readonly string _connectionString;
    private readonly ILogger<RedisRunLock> _logger;
    private readonly SemaphoreSlim _connecting = new(1, 1);
    private IConnectionMultiplexer? _connection;

    public RedisRunLock(string connectionString, ILogger<RedisRunLock> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        var db = (await ConnectAsync(ct)).GetDatabase();
        var token = Guid.NewGuid().ToString("N");

        var acquired = await db.StringSetAsync(key, token, ttl, When.NotExists);
        if (!acquired) return null;

        return new Lease(db, key, token, _logger);
    }

    private async Task<IConnectionMultiplexer> ConnectAsync(CancellationToken ct)
    {
        if (_connection is { IsConnected: true }) return _connection;

        await _connecting.WaitAsync(ct);
        try
        {
            if (_connection is { IsConnected: true }) return _connection;

            if (_connection is not null) await _connection.DisposeAsync();

            var options = ConfigurationOptions.Parse(_connectionString);
            options.AbortOnConnectFail = false;
            _connection = await ConnectionMultiplexer.ConnectAsync(options);
            return _connection;
        }
        finally
        {
            _connecting.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
        _connecting.Dispose();
    }

    private sealed class Lease(IDatabase db, string key, string token, ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await db.ScriptEvaluateAsync(ReleaseScript, [new RedisKey(key)], [new RedisValue(token)]);
            }
            catch (RedisException ex)
            {
                // The TTL releases it eventually. Worth a line in the log and nothing more.
                logger.LogWarning(ex, "Could not release the run lock {Key}; it expires on its own", key);
            }
        }
    }
}
