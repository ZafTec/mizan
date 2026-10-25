using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Services;

/// <summary>
/// Resolves a user's billing entitlement from the subscriptions table, cached via
/// HybridCache (L1 in-proc + L2 Redis) like <see cref="UserStatusService"/>. The
/// webhook pipeline invalidates the cache on subscription changes.
/// </summary>
public class EntitlementService : IEntitlementService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };

    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;
    private readonly IRuntimeOptions<PaddleOptions> _settings;

    public EntitlementService(IMizanDbContext context, HybridCache cache, IRuntimeOptions<PaddleOptions> settings)
    {
        _context = context;
        _cache = cache;
        _settings = settings;
    }

    public async Task<Entitlement> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(
            $"entitlement:{userId}",
            userId,
            LoadAsync,
            CacheOptions,
            tags: new[] { CacheTags.Entitlement(userId) },
            cancellationToken: cancellationToken);
    }

    public async Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveByTagAsync(CacheTags.Entitlement(userId), cancellationToken);
    }

    private async ValueTask<Entitlement> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sub = await _context.Subscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Status, s.IsLifetime, s.CurrentPeriodEnd })
            .FirstOrDefaultAsync(cancellationToken);

        if (sub is null)
        {
            return Entitlement.Free;
        }

        if (sub.IsLifetime)
        {
            return new Entitlement("lifetime", true, null);
        }

        var graceDays = (await _settings.GetAsync(cancellationToken)).GraceDays;
        var isPro = IsPro(sub.Status, sub.CurrentPeriodEnd, DateTime.UtcNow, graceDays);

        return isPro
            ? new Entitlement("pro", true, sub.CurrentPeriodEnd)
            : Entitlement.Free;
    }

    public async Task<IReadOnlySet<Guid>> GetProUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new HashSet<Guid>();

        var subscriptions = await _context.Subscriptions.AsNoTracking()
            .Where(s => userIds.Contains(s.UserId))
            .Select(s => new { s.UserId, s.Status, s.IsLifetime, s.CurrentPeriodEnd })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var graceDays = (await _settings.GetAsync(cancellationToken)).GraceDays;
        return subscriptions
            .Where(s => s.IsLifetime || IsPro(s.Status, s.CurrentPeriodEnd, now, graceDays))
            .Select(s => s.UserId)
            .ToHashSet();
    }

    private static bool IsPro(string status, DateTime? currentPeriodEnd, DateTime now, int graceDays) => status switch
    {
        "active" or "trialing" => true,
        "past_due" => currentPeriodEnd is not null && now < currentPeriodEnd.Value.AddDays(graceDays),
        "canceled" => currentPeriodEnd is not null && now < currentPeriodEnd.Value,
        _ => false
    };
}
