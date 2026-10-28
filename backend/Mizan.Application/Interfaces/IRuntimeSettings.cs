using Mizan.Domain.Settings;

namespace Mizan.Application.Interfaces;

/// <summary>A setting as the admin area sees it. <see cref="Value"/> is always null for a secret.</summary>
public sealed record RuntimeSettingState(
    RuntimeSettingDefinition Definition,
    string? Value,
    bool HasValue,
    string Source,
    DateTime? UpdatedAt,
    Guid? UpdatedByUserId);

/// <summary>
/// The settings an administrator changes in the admin area (docs/ADMIN.md#runtime-settings). The
/// environment never sets them: a key holds its database value, else the catalog's default, and
/// clearing it goes back to the default. Secrets are encrypted at rest and never leave Infrastructure decrypted: code that
/// needs one reads it through <see cref="IRuntimeOptions{TOptions}"/>.
/// </summary>
public interface IRuntimeSettings
{
    /// <summary>
    /// The current value of a non-secret setting: the database value, else the default. Throws
    /// for a secret, which is never handed out as text.
    /// </summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Every defined setting with its current value and where it comes from.</summary>
    Task<IReadOnlyList<RuntimeSettingState>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a value already checked by <see cref="RuntimeSettingDefinitions.Validate"/>. Every API instance sees it within a second.</summary>
    Task SetAsync(string key, string value, Guid? updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Removes the database value so the default applies again. False when there was none.</summary>
    Task<bool> ClearAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// An options object resolved at use time from the runtime settings of its section and their
/// defaults. Read it per call rather than holding the result, so a change in the
/// admin area applies to the next send without a restart.
/// </summary>
public interface IRuntimeOptions<TOptions>
    where TOptions : class
{
    ValueTask<TOptions> GetAsync(CancellationToken cancellationToken = default);
}
