using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Services;

public class AchievementCatalogue : IAchievementCatalogue
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(12),
        LocalCacheExpiration = TimeSpan.FromMinutes(15),
    };

    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public AchievementCatalogue(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Achievement>> MeasurableAsync(CancellationToken cancellationToken = default) =>
        await _cache.GetOrCreateAsync(
            "achievements:measurable",
            _context,
            static async (context, ct) => (IReadOnlyList<Achievement>)await context.Achievements
                .AsNoTracking()
                .Where(a => a.CriteriaType != null)
                .OrderBy(a => a.Threshold)
                .ToListAsync(ct),
            CacheOptions,
            tags: [CacheTags.Achievements],
            cancellationToken: cancellationToken);

    public Task InvalidateAsync(CancellationToken cancellationToken = default) =>
        _cache.RemoveByTagAsync(CacheTags.Achievements, cancellationToken).AsTask();
}
