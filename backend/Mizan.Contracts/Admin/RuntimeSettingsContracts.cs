namespace Mizan.Contracts.Admin;

/// <summary>Where a setting's current value comes from.</summary>
public static class RuntimeSettingSources
{
    /// <summary>An administrator set it in the admin area; it overrides configuration.</summary>
    public const string Database = "database";

    /// <summary>Environment or appsettings, or the built-in default when neither sets it.</summary>
    public const string Configuration = "configuration";

    /// <summary>Nothing sets it.</summary>
    public const string Unset = "unset";
}

/// <summary>
/// One runtime setting as GET /api/admin/settings returns it. A secret never carries
/// <see cref="Value"/>; <see cref="HasValue"/> says whether one is set.
/// </summary>
public record RuntimeSettingDto
{
    /// <summary>The dotted key, such as <c>Smtp.Host</c>: the configuration path with dots for colons.</summary>
    public string Key { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>string, int, bool or enum.</summary>
    public string Type { get; init; } = "string";

    /// <summary>text, host, url, email or json.</summary>
    public string Format { get; init; } = "text";

    public bool IsSecret { get; init; }
    public bool AllowEmpty { get; init; }
    public IReadOnlyList<string> AllowedValues { get; init; } = [];
    public long? Min { get; init; }
    public long? Max { get; init; }

    /// <summary>The current value. Always null for a secret.</summary>
    public string? Value { get; init; }

    public bool HasValue { get; init; }

    /// <summary>database, configuration or unset (<see cref="RuntimeSettingSources"/>).</summary>
    public string Source { get; init; } = RuntimeSettingSources.Unset;

    /// <summary>When the database value was set. Null when the value comes from configuration.</summary>
    public DateTime? UpdatedAt { get; init; }

    public Guid? UpdatedByUserId { get; init; }
}

public record RuntimeSettingGroupDto(string Id, string Title, IReadOnlyList<RuntimeSettingDto> Settings);

/// <summary>Every runtime setting, in the sections of the admin settings page.</summary>
public record RuntimeSettingsDto(IReadOnlyList<RuntimeSettingGroupDto> Groups);

/// <summary>Body of PUT /api/admin/settings/{key}. Booleans are true or false; numbers are whole numbers.</summary>
public record SetRuntimeSettingRequest(string Value);

/// <summary>What POST /api/admin/settings/smtp/test queued: the outbox job and the address it goes to.</summary>
public record SmtpTestResult(Guid JobId, string To);

/// <summary>
/// What POST /api/admin/settings/ai/test found. <see cref="ErrorClass"/> names the failure
/// (not_configured, quota_exceeded, timeout, unreachable, http_401 and so on) without the
/// provider's body.
/// </summary>
public record AiProviderTestResult(bool Reachable, string? ErrorClass, string? Model, int LatencyMs);
