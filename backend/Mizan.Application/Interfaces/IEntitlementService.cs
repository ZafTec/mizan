namespace Mizan.Application.Interfaces;

/// <summary>
/// A user's resolved billing entitlement. <see cref="IsPro"/> is the single flag
/// enforcement should gate on; it already accounts for lifetime, trial, grace, and
/// cancel-until-period-end. Source of truth is the backend `subscriptions` table.
/// </summary>
public record Entitlement(string Plan, bool IsPro, DateTime? AccessUntil)
{
    public static Entitlement Free { get; } = new("free", false, null);
}

public interface IEntitlementService
{
    Task<Entitlement> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}
