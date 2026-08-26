using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Streaks;

namespace Mizan.Infrastructure.Services;

public class UserClock : IUserClock
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(6),
        LocalCacheExpiration = TimeSpan.FromMinutes(10),
    };

    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public UserClock(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<string> TimeZoneIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _cache.GetOrCreateAsync(
            $"user-zone:{userId}",
            (Context: _context, UserId: userId),
            static async (state, ct) =>
                await state.Context.Users.AsNoTracking()
                    .Where(u => u.Id == state.UserId)
                    .Select(u => u.TimeZoneId)
                    .FirstOrDefaultAsync(ct)
                ?? StreakClock.DefaultTimeZone,
            CacheOptions,
            tags: [CacheTags.UserZone(userId)],
            cancellationToken: cancellationToken);

    public async Task<DateOnly> TodayAsync(Guid userId, CancellationToken cancellationToken = default) =>
        StreakClock.Today(await TimeZoneIdAsync(userId, cancellationToken), DateTimeOffset.UtcNow);

    public async Task<StreakState> EvaluateAsync(
        Guid userId,
        int currentCount,
        int longestCount,
        DateOnly? lastActivityDate,
        int freezesAvailable,
        CancellationToken cancellationToken = default) =>
        StreakClock.Evaluate(
            currentCount,
            longestCount,
            lastActivityDate,
            freezesAvailable,
            await TimeZoneIdAsync(userId, cancellationToken),
            DateTimeOffset.UtcNow);
}
