using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Identity;

public class UserCacheInvalidator : IUserCacheInvalidator
{
    private readonly HybridCache _cache;

    public UserCacheInvalidator(HybridCache cache) => _cache = cache;

    public Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _cache.RemoveByTagAsync(
            [CacheTags.UserStatus(userId), CacheTags.UserZone(userId)],
            cancellationToken).AsTask();
}
