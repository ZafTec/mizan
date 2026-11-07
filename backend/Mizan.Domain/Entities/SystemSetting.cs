namespace Mizan.Domain.Entities;

/// <summary>
/// One runtime setting an administrator set in the admin area, overriding the configuration value
/// of the same key (docs/ADMIN.md#runtime-settings). One row per key; no row means the
/// configuration value applies.
/// </summary>
public class SystemSetting
{
    /// <summary>A key from <see cref="Settings.RuntimeSettingDefinitions"/>, such as <c>Smtp.Host</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The value as text. When <see cref="IsSecret"/> is set, this is the protected payload, never the secret.</summary>
    public string Value { get; set; } = string.Empty;

    public bool IsSecret { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
