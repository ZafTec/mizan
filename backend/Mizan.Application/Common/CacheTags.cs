namespace Mizan.Application.Common;

// Shared tag constants for HybridCache invalidation. Keeping them in one place
// prevents drift between producers (cache writers) and invalidators.
public static class CacheTags
{
    public const string Jwks = "jwks";
    public const string Foods = "foods";

    public static string UserStatus(Guid userId) => $"user:{userId}";
}
