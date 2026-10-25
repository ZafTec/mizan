namespace Mizan.Contracts.Mcp;

/// <summary>Claim names that carry a connection's grant from authentication to the handlers.</summary>
public static class GrantClaims
{
    public const string GrantId = "grant_id";
    public const string Client = "grant_client";
    public const string Scopes = "grant_scopes";
    public const string HouseholdMode = "grant_household_mode";
    public const string Households = "grant_households";
}
