using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Domain.Settings;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Telegram;

namespace Mizan.Infrastructure.Settings;

/// <summary>
/// Reads and writes <c>system_settings</c>. A secret is protected with ASP.NET Core Data Protection
/// before it is stored and is reported only as set or not set; the decrypted value goes no further
/// than <see cref="RuntimeOptions{TOptions}"/>, and for the bot's token and webhook secret the
/// service-key-only <see cref="ServiceSettings"/> (docs/ADMIN.md#runtime-settings).
/// </summary>
internal sealed class RuntimeSettings : IRuntimeSettings
{
    private readonly MizanDbContext _db;
    private readonly RuntimeSettingsCache _cache;
    private readonly ITelegramBotNotifier _bot;

    public RuntimeSettings(MizanDbContext db, RuntimeSettingsCache cache, ITelegramBotNotifier bot)
    {
        _db = db;
        _cache = cache;
        _bot = bot;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var definition = Require(key);
        if (definition.IsSecret)
        {
            throw new InvalidOperationException($"{definition.Key} is a secret and is read through its options, not as text.");
        }

        var snapshot = await _cache.GetAsync(cancellationToken);
        return snapshot.Rows.TryGetValue(definition.Key, out var row) ? row.Value : definition.Default;
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

            var hasValue = !string.IsNullOrEmpty(definition.Default);
            states.Add(new RuntimeSettingState(
                definition,
                definition.IsSecret || !hasValue ? null : definition.Default,
                hasValue,
                hasValue ? RuntimeSettingSources.Default : RuntimeSettingSources.Unset,
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

        await ChangedAsync(definition, cancellationToken);
    }

    public async Task<bool> ClearAsync(string key, CancellationToken cancellationToken = default)
    {
        var definition = Require(key);
        var removed = await _db.SystemSettings.Where(s => s.Key == definition.Key).ExecuteDeleteAsync(cancellationToken);
        await ChangedAsync(definition, cancellationToken);
        return removed > 0;
    }

    /// <summary>
    /// Every API instance reloads within a second. The bot service holds no database, so it is told
    /// directly when one of its settings changes, and re-registers its webhook if it needs to.
    /// </summary>
    private async Task ChangedAsync(RuntimeSettingDefinition definition, CancellationToken cancellationToken)
    {
        await _cache.PublishChangeAsync();
        if (definition.Group == RuntimeSettingGroups.Telegram) await _bot.SettingsChangedAsync(cancellationToken);
    }

    private static RuntimeSettingDefinition Require(string key) =>
        RuntimeSettingDefinitions.Find(key)
        ?? throw new ArgumentException($"There is no runtime setting named '{key}'.", nameof(key));
}
