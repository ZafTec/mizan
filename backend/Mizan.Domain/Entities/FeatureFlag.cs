namespace Mizan.Domain.Entities;

/// <summary>
/// An administrator's change to a flag's default. The catalog in
/// <see cref="Features.FeatureFlags"/> carries the shipped default; a row exists
/// only once someone has changed it, so a new flag needs no seed (docs/ADMIN.md).
/// </summary>
public class FeatureFlag
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool DefaultEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>One household's own answer for one flag. It wins over the default.</summary>
public class FeatureFlagOverride
{
    public Guid Id { get; set; }
    public string FlagKey { get; set; } = string.Empty;
    public Guid HouseholdId { get; set; }
    public bool Enabled { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public virtual Household? Household { get; set; }
}
