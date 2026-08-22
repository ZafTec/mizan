namespace Mizan.Application.Interfaces;

/// <summary>
/// Drops the cached access status for one user. Anything that changes whether
/// a user may act - verification, ban, role - has to call this or the change
/// takes up to the cache TTL to bite.
/// </summary>
public interface IUserCacheInvalidator
{
    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}
