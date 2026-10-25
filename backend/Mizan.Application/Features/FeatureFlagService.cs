using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Features;

namespace Mizan.Application.Features;

/// <summary>
/// Reads flags for <see cref="FeatureGate"/>. The stored defaults are one small
/// map and each household's overrides another, both cached for a minute under
/// <see cref="CacheTags.FeatureFlags"/>, which every flag write clears.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlags
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(1),
        LocalCacheExpiration = TimeSpan.FromSeconds(30),
    };

    private static readonly string[] Tags = [CacheTags.FeatureFlags];

    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public FeatureFlagService(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string key, Guid? householdId, CancellationToken cancellationToken = default)
    {
        var definition = FeatureFlags.Find(key)
            ?? throw new ArgumentException($"'{key}' is not a feature flag in the catalog.", nameof(key));

        var defaults = await _cache.GetOrCreateAsync(
            "feature-flags:defaults",
            async ct => await _context.FeatureFlags.AsNoTracking()
                .ToDictionaryAsync(f => f.Key, f => f.DefaultEnabled, ct),
            CacheOptions, Tags, cancellationToken);

        bool? householdOverride = null;
        if (householdId is { } household)
        {
            var overrides = await _cache.GetOrCreateAsync(
                $"feature-flags:household:{household}",
                async ct => await _context.FeatureFlagOverrides.AsNoTracking()
                    .Where(o => o.HouseholdId == household)
                    .ToDictionaryAsync(o => o.FlagKey, o => o.Enabled, ct),
                CacheOptions, Tags, cancellationToken);
            if (overrides.TryGetValue(key, out var value)) householdOverride = value;
        }

        return FeatureFlags.Resolve(
            definition.DefaultEnabled,
            defaults.TryGetValue(key, out var stored) ? stored : null,
            householdOverride);
    }
}

/// <summary>
/// The one check every gated path makes. A feature that is off answers 403
/// <c>feature_disabled</c> with <see cref="Exceptions.FeatureDisabledException.DefaultMessage"/>,
/// whether the call came from the web, MCP, Telegram or the assistant's tools.
/// </summary>
public sealed class FeatureGate
{
    private readonly IFeatureFlags _flags;
    private readonly ActiveHousehold _households;
    private readonly ICurrentUserService _currentUser;

    public FeatureGate(IFeatureFlags flags, ActiveHousehold households, ICurrentUserService currentUser)
    {
        _flags = flags;
        _households = households;
        _currentUser = currentUser;
    }

    /// <summary>For the signed-in caller, in their active household.</summary>
    public Task EnsureEnabledAsync(string key, CancellationToken cancellationToken = default) =>
        EnsureEnabledForUserAsync(key, _currentUser.UserId, cancellationToken);

    /// <summary>For a user the request is about but not signed in as, such as a Mini App sign-in.</summary>
    public async Task EnsureEnabledForUserAsync(string key, Guid? userId, CancellationToken cancellationToken = default)
    {
        var household = userId is { } id ? await _households.ForUserAsync(id, cancellationToken) : null;
        if (!await _flags.IsEnabledAsync(key, household, cancellationToken))
        {
            throw new Exceptions.FeatureDisabledException(key);
        }
    }
}
