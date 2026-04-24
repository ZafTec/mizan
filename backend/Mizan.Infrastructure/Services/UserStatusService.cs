using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Services;

public class UserStatusService : IUserStatusService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(2),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };

    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public UserStatusService(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<UserAccessStatus> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(
            $"user-status:{userId}",
            userId,
            LoadAsync,
            CacheOptions,
            tags: new[] { CacheTags.UserStatus(userId) },
            cancellationToken: cancellationToken);
    }

    private async ValueTask<UserAccessStatus> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.EmailVerified, u.Banned, u.BanExpires, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        return user == null
            ? new UserAccessStatus(false, false, false)
            : new UserAccessStatus(
                true,
                user.EmailVerified,
                user.Banned && (!user.BanExpires.HasValue || user.BanExpires > DateTime.UtcNow),
                user.Role);
    }
}
