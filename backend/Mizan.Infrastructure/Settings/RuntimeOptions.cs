using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Domain.Settings;

namespace Mizan.Infrastructure.Settings;

/// <summary>A configuration section whose keys the admin area can override.</summary>
internal interface IRuntimeOptionsSection
{
    string Section { get; }

    /// <summary>The value configuration gives a key (the built-in default when nothing sets it), as text.</summary>
    string? ConfigurationValue(RuntimeSettingDefinition definition);
}

/// <summary>
/// The configuration-bound <typeparamref name="TOptions"/> with every stored runtime setting of its
/// section laid over it. Precedence is the database value when there is one, else configuration.
/// The result is rebuilt only when the settings snapshot or the configuration changes, and is shared
/// between callers, so nobody may change it.
/// </summary>
public sealed class RuntimeOptions<TOptions> : IRuntimeOptions<TOptions>, IRuntimeOptionsSection
    where TOptions : class
{
    private readonly IOptionsMonitor<TOptions> _monitor;
    private readonly RuntimeSettingsCache _cache;
    private readonly ILogger<RuntimeOptions<TOptions>> _logger;
    private Built? _built;

    public RuntimeOptions(
        string section,
        IOptionsMonitor<TOptions> monitor,
        RuntimeSettingsCache cache,
        ILogger<RuntimeOptions<TOptions>> logger)
    {
        Section = section;
        _monitor = monitor;
        _cache = cache;
        _logger = logger;
    }

    public string Section { get; }

    public async ValueTask<TOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _cache.GetAsync(cancellationToken);
        var configured = _monitor.CurrentValue;

        var built = Volatile.Read(ref _built);
        if (built is not null && ReferenceEquals(built.Snapshot, snapshot) && ReferenceEquals(built.Configured, configured))
        {
            return built.Value;
        }

        var value = Overlay(configured, snapshot);
        Volatile.Write(ref _built, new Built(snapshot, configured, value));
        return value;
    }

    public string? ConfigurationValue(RuntimeSettingDefinition definition)
    {
        object? current = _monitor.CurrentValue;
        foreach (var name in definition.Key.Split('.').Skip(1))
        {
            if (current is null) return null;
            var property = current.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null) return null;
            current = property.GetValue(current);
        }

        return current switch
        {
            null => null,
            string text => text,
            bool flag => flag ? "true" : "false",
            IFormattable formattable when current is not Enum => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => current.ToString(),
        };
    }

    private TOptions Overlay(TOptions configured, RuntimeSettingsSnapshot snapshot)
    {
        var prefix = Section + ".";
        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in snapshot.Rows.Values)
        {
            // Only catalogued keys, and never a secret the key ring could not open.
            if (row.Value is null || !row.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var definition = RuntimeSettingDefinitions.Find(row.Key);
            if (definition is null) continue;
            overrides[definition.ConfigurationPath] = row.Value;
        }

        // A copy, because the monitor's instance is shared with everything that reads configuration.
        var copy = JsonSerializer.Deserialize<TOptions>(JsonSerializer.SerializeToUtf8Bytes(configured))!;
        if (overrides.Count == 0) return copy;

        try
        {
            new ConfigurationBuilder().AddInMemoryCollection(overrides).Build().GetSection(Section).Bind(copy);
            return copy;
        }
        catch (InvalidOperationException ex)
        {
            // Values are checked against the catalog before they are stored, so this means the catalog
            // and the options type disagree. Configuration is the safe answer.
            _logger.LogError(ex, "Runtime settings for {Section} could not be applied; using configuration", Section);
            return JsonSerializer.Deserialize<TOptions>(JsonSerializer.SerializeToUtf8Bytes(configured))!;
        }
    }

    private sealed record Built(RuntimeSettingsSnapshot Snapshot, TOptions Configured, TOptions Value);
}
