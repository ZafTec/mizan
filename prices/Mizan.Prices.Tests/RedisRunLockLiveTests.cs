using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mizan.Prices.Scheduling;
using StackExchange.Redis;

namespace Mizan.Prices.Tests;

/// <summary>
/// The one test that needs a Redis. It passes without one, saying so on the
/// test output, because the unit suite must run on any machine; CI and the
/// Compose test service have Redis and exercise it for real.
/// </summary>
public class RedisRunLockLiveTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TEST_REDIS_CONNECTION") ?? "localhost:6379,defaultDatabase=9,connectTimeout=1500,abortConnect=false";

    [Fact]
    public async Task TwoWorkers_CannotHoldTheSameSourceLock_AndTheLeaseIsReleasedOnDispose()
    {
        if (!await IsReachableAsync())
        {
            // No Redis here. Nothing to prove and nothing wrong.
            return;
        }

        var key = $"prices:run:test-{Guid.NewGuid():N}";
        await using var first = new RedisRunLock(ConnectionString, NullLogger<RedisRunLock>.Instance);
        await using var second = new RedisRunLock(ConnectionString, NullLogger<RedisRunLock>.Instance);

        var lease = await first.TryAcquireAsync(key, TimeSpan.FromMinutes(1), CancellationToken.None);
        lease.Should().NotBeNull();

        (await second.TryAcquireAsync(key, TimeSpan.FromMinutes(1), CancellationToken.None)).Should().BeNull();

        await lease!.DisposeAsync();

        var afterRelease = await second.TryAcquireAsync(key, TimeSpan.FromMinutes(1), CancellationToken.None);
        afterRelease.Should().NotBeNull();
        await afterRelease!.DisposeAsync();
    }

    private static async Task<bool> IsReachableAsync()
    {
        try
        {
            var options = ConfigurationOptions.Parse(ConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 1500;
            await using var connection = await ConnectionMultiplexer.ConnectAsync(options);
            await connection.GetDatabase().PingAsync();
            return true;
        }
        catch (RedisException)
        {
            return false;
        }
    }
}
