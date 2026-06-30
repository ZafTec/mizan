using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
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
    private readonly PaddleOptions _options;

    public EntitlementService(IMizanDbContext context, HybridCache cache, IOptions<PaddleOptions> options)
    {
        _context = context;
        _cache = cache;
        _options = options.Value;
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

        var now = DateTime.UtcNow;
        var isPro = sub.Status switch
        {
            "active" or "trialing" => true,
            "past_due" => sub.CurrentPeriodEnd is not null && now < sub.CurrentPeriodEnd.Value.AddDays(_options.GraceDays),
            "canceled" => sub.CurrentPeriodEnd is not null && now < sub.CurrentPeriodEnd.Value,
            _ => false
        };

        return isPro
            ? new Entitlement("pro", true, sub.CurrentPeriodEnd)
            : Entitlement.Free;
    }
}
