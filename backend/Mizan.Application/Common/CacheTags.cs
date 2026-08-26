namespace Mizan.Application.Common;

// Shared tag constants for HybridCache invalidation. Keeping them in one place
// prevents drift between producers (cache writers) and invalidators.
public static class CacheTags
{
    public const string Jwks = "jwks";
    public const string Foods = "foods";

    /// <summary>The achievement catalogue - seed data, read on every log, edited a few times a year.</summary>
    public const string Achievements = "achievements";

    public static string UserStatus(Guid userId) => $"user:{userId}";
    public static string Entitlement(Guid userId) => $"entitlement:{userId}";

    /// <summary>
    /// The user's timezone, read on every log write. Same lifetime as their
    /// status, and cleared by the same invalidator.
    /// </summary>
    public static string UserZone(Guid userId) => $"user-zone:{userId}";
}
