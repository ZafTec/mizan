namespace Mizan.Contracts.Mcp;

/// <summary>Claim names for a session an administrator opened to view the site as a user.</summary>
public static class ImpersonationClaims
{
    public const string Impersonator = "impersonator_id";
    public const string Expires = "impersonation_expires";
}
