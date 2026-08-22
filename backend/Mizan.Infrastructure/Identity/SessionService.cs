using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;

namespace Mizan.Infrastructure.Identity;

public class SessionService : ISessionService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// A revoked session must stop working promptly, and the cache is only an
    /// optimisation over an indexed single-row lookup, so the window is short.
    /// </summary>
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(2),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };

    /// <summary>LastSeenAt is a courtesy for the sessions screen, not a clock.</summary>
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(15);

    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public SessionService(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<string> CreateAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var token = SecureToken.Generate();
        var now = DateTime.UtcNow;

        _context.UserSessions.Add(new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = SecureToken.Hash(token),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.Add(Lifetime),
            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 512),
        });

        await _context.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<Guid?> ResolveAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var hash = SecureToken.Hash(token);
        var resolved = await _cache.GetOrCreateAsync(
            $"session:{hash}",
            (context: _context, hash),
            static async (state, ct) => await state.context.UserSessions
                .AsNoTracking()
                .Where(s => s.TokenHash == state.hash && s.ExpiresAt > DateTime.UtcNow)
                .Select(s => new ResolvedSession(s.UserId, s.LastSeenAt))
                .FirstOrDefaultAsync(ct),
            CacheOptions,
            cancellationToken: cancellationToken);

        if (resolved is null) return null;

        if (DateTime.UtcNow - resolved.LastSeenAt > TouchInterval)
        {
            await TouchAsync(hash, cancellationToken);
        }

        return resolved.UserId;
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;

        var hash = SecureToken.Hash(token);
        await _context.UserSessions
            .Where(s => s.TokenHash == hash)
            .ExecuteDeleteAsync(cancellationToken);
        await _cache.RemoveAsync($"session:{hash}", cancellationToken);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var hashes = await _context.UserSessions
            .Where(s => s.UserId == userId)
            .Select(s => s.TokenHash)
            .ToListAsync(cancellationToken);

        if (hashes.Count == 0) return;

        await _context.UserSessions
            .Where(s => s.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await EvictAsync(hashes, cancellationToken);
    }

    public async Task RevokeAllExceptAsync(
        Guid userId,
        string keepToken,
        CancellationToken cancellationToken = default)
    {
        var keepHash = SecureToken.Hash(keepToken);
        var hashes = await _context.UserSessions
            .Where(s => s.UserId == userId && s.TokenHash != keepHash)
            .Select(s => s.TokenHash)
            .ToListAsync(cancellationToken);

        if (hashes.Count == 0) return;

        await _context.UserSessions
            .Where(s => s.UserId == userId && s.TokenHash != keepHash)
            .ExecuteDeleteAsync(cancellationToken);

        await EvictAsync(hashes, cancellationToken);
    }

    public async Task RevokeByIdAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var hash = await _context.UserSessions
            .Where(s => s.Id == sessionId && s.UserId == userId)
            .Select(s => s.TokenHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (hash is null) return;

        await _context.UserSessions
            .Where(s => s.Id == sessionId && s.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await _cache.RemoveAsync($"session:{hash}", cancellationToken);
    }

    private async Task TouchAsync(string hash, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await _context.UserSessions
            .Where(s => s.TokenHash == hash)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastSeenAt, now)
                .SetProperty(x => x.ExpiresAt, now.Add(Lifetime)),
                cancellationToken);
        await _cache.RemoveAsync($"session:{hash}", cancellationToken);
    }

    private async Task EvictAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
    {
        foreach (var hash in hashes)
        {
            await _cache.RemoveAsync($"session:{hash}", cancellationToken);
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    private sealed record ResolvedSession(Guid UserId, DateTime LastSeenAt);
}
