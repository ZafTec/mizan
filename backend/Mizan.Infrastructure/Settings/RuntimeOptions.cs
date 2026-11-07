using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;
using Mizan.Domain.Settings;

namespace Mizan.Infrastructure.Settings;

/// <summary>
/// <typeparamref name="TOptions"/> built from the settings catalog alone: each key of its section takes
/// the stored value when there is one, else the catalog's default. Configuration and the environment
/// are not read (docs/ADMIN.md#runtime-settings). The result is rebuilt only when the settings
/// snapshot changes and is shared between callers, so nobody may change it.
/// </summary>
public sealed class RuntimeOptions<TOptions> : IRuntimeOptions<TOptions>
    where TOptions : class, new()
{
    private readonly RuntimeSettingsCache _cache;
    private readonly ILogger<RuntimeOptions<TOptions>> _logger;
    private Built? _built;

    public RuntimeOptions(string section, RuntimeSettingsCache cache, ILogger<RuntimeOptions<TOptions>> logger)
    {
        Section = section;
        _cache = cache;
        _logger = logger;
    }

    public string Section { get; }

    public async ValueTask<TOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _cache.GetAsync(cancellationToken);

        var built = Volatile.Read(ref _built);
        if (built is not null && ReferenceEquals(built.Snapshot, snapshot))
        {
            return built.Value;
        }

        var value = Build(snapshot);
        Volatile.Write(ref _built, new Built(snapshot, value));
        return value;
    }

    private TOptions Build(RuntimeSettingsSnapshot snapshot)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in RuntimeSettingDefinitions.All)
        {
            if (!string.Equals(definition.Section, Section, StringComparison.OrdinalIgnoreCase)) continue;

            // A secret the key ring could not open reads as unset rather than as its ciphertext.
            var value = snapshot.Rows.TryGetValue(definition.Key, out var row) ? row.Value : definition.Default;
            if (value is not null) values[definition.ConfigurationPath] = value;
        }

        var options = new TOptions();
        try
        {
            new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection(Section).Bind(options);
            return options;
        }
        catch (InvalidOperationException ex)
        {
            // Values are checked against the catalog before they are stored, so this means the catalog
            // and the options type disagree. The type's own defaults are the safe answer.
            _logger.LogError(ex, "Runtime settings for {Section} could not be applied; using defaults", Section);
            return new TOptions();
        }
    }

    private sealed record Built(RuntimeSettingsSnapshot Snapshot, TOptions Value);
}
