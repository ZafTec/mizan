using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Settings;
using Mizan.Infrastructure.Data;

namespace Mizan.Infrastructure.Settings;

/// <summary>
/// Reads and writes <c>system_settings</c>. A secret is protected with ASP.NET Core Data Protection
/// before it is stored and is reported only as set or not set; the decrypted value goes no further
/// than <see cref="RuntimeOptions{TOptions}"/> (docs/ADMIN.md#runtime-settings).
/// </summary>
internal sealed class RuntimeSettings : IRuntimeSettings
{
    private readonly MizanDbContext _db;
    private readonly RuntimeSettingsCache _cache;
    private readonly Dictionary<string, IRuntimeOptionsSection> _sections;

    public RuntimeSettings(MizanDbContext db, RuntimeSettingsCache cache, IEnumerable<IRuntimeOptionsSection> sections)
    {
        _db = db;
        _cache = cache;
        _sections = sections.ToDictionary(s => s.Section, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var definition = Require(key);
        if (definition.IsSecret)
        {
            throw new InvalidOperationException($"{definition.Key} is a secret and is read through its options, not as text.");
        }

        var snapshot = await _cache.GetAsync(cancellationToken);
        return snapshot.Rows.TryGetValue(definition.Key, out var row) ? row.Value : ConfigurationValue(definition);
    }

    public async Task<IReadOnlyList<RuntimeSettingState>> ListAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _cache.GetAsync(cancellationToken);
        var states = new List<RuntimeSettingState>(RuntimeSettingDefinitions.All.Count);
        foreach (var definition in RuntimeSettingDefinitions.All)
        {
            if (snapshot.Rows.TryGetValue(definition.Key, out var row))
            {
                states.Add(new RuntimeSettingState(
                    definition,
                    definition.IsSecret ? null : row.Value,
                    !string.IsNullOrEmpty(row.Value),
                    RuntimeSettingSources.Database,
                    row.UpdatedAt,
                    row.UpdatedByUserId));
                continue;
            }

            var configured = ConfigurationValue(definition);
            var hasValue = !string.IsNullOrEmpty(configured);
            states.Add(new RuntimeSettingState(
                definition,
                definition.IsSecret || !hasValue ? null : configured,
                hasValue,
                hasValue ? RuntimeSettingSources.Configuration : RuntimeSettingSources.Unset,
                null,
                null));
        }

        return states;
    }

    public async Task SetAsync(string key, string value, Guid? updatedByUserId, CancellationToken cancellationToken = default)
    {
        var definition = Require(key);
        var stored = definition.IsSecret ? _cache.Protector.Protect(value) : value;
        var now = DateTime.UtcNow;

        // One statement, so two administrators saving the same key at once both succeed and the
        // later one wins, rather than one of them hitting the primary key.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO system_settings (key, value, is_secret, updated_at, updated_by_user_id)
            VALUES ({definition.Key}, {stored}, {definition.IsSecret}, {now}, {updatedByUserId})
            ON CONFLICT (key) DO UPDATE
            SET value = EXCLUDED.value, is_secret = EXCLUDED.is_secret,
                updated_at = EXCLUDED.updated_at, updated_by_user_id = EXCLUDED.updated_by_user_id
            """,
            cancellationToken);

        await _cache.PublishChangeAsync();
    }

    public async Task<bool> ClearAsync(string key, CancellationToken cancellationToken = default)
    {
        var definition = Require(key);
        var removed = await _db.SystemSettings.Where(s => s.Key == definition.Key).ExecuteDeleteAsync(cancellationToken);
        await _cache.PublishChangeAsync();
        return removed > 0;
    }

    private string? ConfigurationValue(RuntimeSettingDefinition definition) =>
        _sections.TryGetValue(definition.Section, out var section) ? section.ConfigurationValue(definition) : null;

    private static RuntimeSettingDefinition Require(string key) =>
        RuntimeSettingDefinitions.Find(key)
        ?? throw new ArgumentException($"There is no runtime setting named '{key}'.", nameof(key));
}
