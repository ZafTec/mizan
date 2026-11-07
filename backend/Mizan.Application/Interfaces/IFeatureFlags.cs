namespace Mizan.Application.Interfaces;

/// <summary>
/// Whether a catalog feature is on for a household: its override when it has
/// one, else the stored default, else the catalog default
/// (<see cref="Domain.Features.FeatureFlags.Resolve"/>). Cached briefly and
/// cleared by every flag write.
/// </summary>
public interface IFeatureFlags
{
    Task<bool> IsEnabledAsync(string key, Guid? householdId, CancellationToken cancellationToken = default);
}
