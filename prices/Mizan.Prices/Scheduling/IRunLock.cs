using System.Collections.Concurrent;

namespace Mizan.Prices.Scheduling;

/// <summary>
/// One run per source at a time, across every worker instance. The lease
/// expires on its own so a crashed worker cannot hold a source hostage.
/// </summary>
public interface IRunLock
{
    /// <summary>The lease, or null when another holder has it.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct);
}

/// <summary>
/// For a single instance, and for tests. Two containers sharing this would
/// each believe they were alone, which is why Program.cs warns when Redis is
/// not configured.
/// </summary>
public sealed class InMemoryRunLock : IRunLock
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _held = new();
    private readonly Func<DateTimeOffset> _now;

    public InMemoryRunLock() : this(() => DateTimeOffset.UtcNow)
    {
    }

    public InMemoryRunLock(Func<DateTimeOffset> now)
    {
        _now = now;
    }

    public bool IsHeld(string key) => _held.TryGetValue(key, out var until) && until > _now();

    public Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        var expiresAt = _now() + ttl;

        // Add when absent, or take over an expired lease. Either way the
        // compare-and-swap is one dictionary operation.
        while (true)
        {
            if (_held.TryAdd(key, expiresAt))
            {
                return Task.FromResult<IAsyncDisposable?>(new Lease(() => _held.TryRemove(new KeyValuePair<string, DateTimeOffset>(key, expiresAt))));
            }

            if (!_held.TryGetValue(key, out var current)) continue;
            if (current > _now()) return Task.FromResult<IAsyncDisposable?>(null);

            if (_held.TryUpdate(key, expiresAt, current))
            {
                return Task.FromResult<IAsyncDisposable?>(new Lease(() => _held.TryRemove(new KeyValuePair<string, DateTimeOffset>(key, expiresAt))));
            }
        }
    }

    private sealed class Lease(Action release) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            release();
            return ValueTask.CompletedTask;
        }
    }
}
