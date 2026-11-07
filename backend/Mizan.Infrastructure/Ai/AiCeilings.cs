using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Ai;

/// <summary>The global ceilings as <see cref="AiQuotaService"/> enforces them, for the admin usage view.</summary>
public sealed class AiCeilings : IAiCeilings
{
    private readonly IRuntimeOptions<AiOptions> _settings;

    public AiCeilings(IRuntimeOptions<AiOptions> settings) => _settings = settings;

    public async Task<AiGlobalCeilings> GetAsync(CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken);
        return new AiGlobalCeilings(options.GlobalDailyTokens, options.GlobalDailyCostMicros);
    }
}
