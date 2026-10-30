using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mizan.Infrastructure.Data;
using StackExchange.Redis;

namespace Mizan.Infrastructure.Settings;

/// <summary>One stored setting, with a secret already decrypted. Never leaves Infrastructure.</summary>
internal sealed record StoredRuntimeSetting(
    string Key,
    string? Value,
    bool IsSecret,
    DateTime UpdatedAt,
    Guid? UpdatedByUserId);

/// <summary>Every stored setting at one moment. Readers compare snapshots by reference to know when to rebuild.</summary>
internal sealed class RuntimeSettingsSnapshot
{
    public RuntimeSettingsSnapshot(IReadOnlyDictionary<string, StoredRuntimeSetting> rows, long loadedAt)
    {
        Rows = rows;
        LoadedAt = loadedAt;
    }

    public IReadOnlyDictionary<string, StoredRuntimeSetting> Rows { get; }

    /// <summary><see cref="Environment.TickCount64"/> when it was read.</summary>
    public long LoadedAt { get; }
}

/// <summary>
/// The <c>system_settings</c> table, held in process for a short time. A write on this instance drops
/// the copy at once and publishes on a Redis channel, so every other instance drops its copy too. The
/// copy also expires on its own: after 30 seconds while the channel is subscribed, and after one second
/// without Redis, so an instance that missed a message is never wrong for long
/// (docs/ADMIN.md#runtime-settings).
/// </summary>
public sealed class RuntimeSettingsCache
{
    public const string Channel = "mizan:runtime-settings:changed";

    /// <summary>The purpose string for the protector. Changing it makes every stored secret unreadable.</summary>
    public const string ProtectorPurpose = "Mizan.RuntimeSettings.v1";

    private static readonly TimeSpan SubscribedLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollingLifetime = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IServiceProvider _services;
    private readonly IDataProtector _protector;
    private readonly ILogger<RuntimeSettingsCache> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private RuntimeSettingsSnapshot? _snapshot;
    private long _generation;
    private int _subscribeAttempted;
    private ISubscriber? _subscriber;
    private volatile bool _subscribed;

    public RuntimeSettingsCache(
        IServiceScopeFactory scopes,
        IServiceProvider services,
        IDataProtectionProvider dataProtection,
        ILogger<RuntimeSettingsCache> logger)
    {
        _scopes = scopes;
        _services = services;
        _protector = dataProtection.CreateProtector(ProtectorPurpose);
        _logger = logger;
    }

    internal IDataProtector Protector => _protector;

    internal async ValueTask<RuntimeSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        await EnsureSubscribedAsync();

        var current = Volatile.Read(ref _snapshot);
        if (current is not null && !Expired(current)) return current;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            current = Volatile.Read(ref _snapshot);
            if (current is not null && !Expired(current)) return current;

            // A write that lands while this load runs bumps the generation; the result is then
            // returned to this caller but not kept, so the next read loads again.
            var generation = Interlocked.Read(ref _generation);
            var loaded = await LoadAsync(cancellationToken);
            if (Interlocked.Read(ref _generation) == generation) Volatile.Write(ref _snapshot, loaded);
            return loaded;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>Drops this instance's copy, so the next read goes to the database.</summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        Volatile.Write(ref _snapshot, null);
    }

    /// <summary>Drops this copy and tells every other instance to drop theirs.</summary>
    internal async Task PublishChangeAsync()
    {
        Invalidate();
        if (_subscriber is null) return;
        try
        {
            await _subscriber.PublishAsync(RedisChannel.Literal(Channel), "changed");
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // The other instances still expire their copies on their own.
            _logger.LogWarning(ex, "Could not announce a runtime setting change over Redis");
        }
    }

    private bool Expired(RuntimeSettingsSnapshot snapshot)
    {
        var lifetime = _subscribed ? SubscribedLifetime : PollingLifetime;
        return Environment.TickCount64 - snapshot.LoadedAt > lifetime.TotalMilliseconds;
    }

    private async Task EnsureSubscribedAsync()
    {
        if (Interlocked.Exchange(ref _subscribeAttempted, 1) == 1) return;

        try
        {
            var redis = _services.GetService<IConnectionMultiplexer>();
            if (redis is null) return;
            var subscriber = redis.GetSubscriber();
            await subscriber.SubscribeAsync(RedisChannel.Literal(Channel), (_, _) => Invalidate());
            _subscriber = subscriber;
            _subscribed = true;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Runtime settings are not subscribed to Redis; each instance rereads them every second");
        }
    }

    private async Task<RuntimeSettingsSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var rows = await db.SystemSettings.AsNoTracking().ToListAsync(cancellationToken);

        var stored = new Dictionary<string, StoredRuntimeSetting>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            stored[row.Key] = new StoredRuntimeSetting(
                row.Key,
                row.IsSecret ? Unprotect(row.Key, row.Value) : row.Value,
                row.IsSecret,
                row.UpdatedAt,
                row.UpdatedByUserId);
        }

        return new RuntimeSettingsSnapshot(stored, Environment.TickCount64);
    }

    private string? Unprotect(string key, string payload)
    {
        try
        {
            return _protector.Unprotect(payload);
        }
        catch (CryptographicException)
        {
            // The key ring that protected it is gone. The setting falls back to configuration until
            // an administrator enters it again; the value itself is never logged.
            _logger.LogWarning("The stored secret {Key} cannot be decrypted with the current key ring; configuration applies", key);
            return null;
        }
    }
}
