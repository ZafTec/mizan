using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Services;

public class UserStatusService : IUserStatusService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
    private readonly IMizanDbContext _context;
    private readonly IMemoryCache _cache;

    public UserStatusService(IMizanDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<UserAccessStatus> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(userId, out var cachedObj) && cachedObj is UserAccessStatus cached)
        {
            return cached;
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.EmailVerified, u.Banned, u.BanExpires, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        var status = user == null
            ? new UserAccessStatus(false, false, false)
            : new UserAccessStatus(
                true,
                user.EmailVerified,
                user.Banned && (!user.BanExpires.HasValue || user.BanExpires > DateTime.UtcNow),
                user.Role);

        _cache.Set(userId, status, CacheDuration);
        return status;
    }
}
