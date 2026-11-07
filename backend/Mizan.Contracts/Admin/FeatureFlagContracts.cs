namespace Mizan.Contracts.Admin;

/// <summary>
/// One catalog flag as GET /api/admin/feature-flags lists it: the default in
/// force (the catalog's until an administrator changes it) and how many
/// households have their own answer.
/// </summary>
public record FeatureFlagDto
{
    public string Key { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool DefaultEnabled { get; init; }

    /// <summary>The default the code ships with, so the page can say when the stored one differs.</summary>
    public bool CatalogDefault { get; init; }
    public int OverrideCount { get; init; }
    public int EnabledOverrides { get; init; }
    public int DisabledOverrides { get; init; }

    /// <summary>When an administrator last changed the default; null while it is the catalog's.</summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>One household's override, from GET /api/admin/feature-flags/{key}/overrides.</summary>
public record FeatureFlagOverrideDto
{
    public Guid HouseholdId { get; init; }
    public string HouseholdName { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public bool Enabled { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? UpdatedByName { get; init; }
}

/// <summary>Body of PUT /api/admin/feature-flags/{key} and PUT /api/admin/feature-flags/{key}/overrides/{householdId}.</summary>
public record SetFeatureFlagRequest(bool Enabled);
