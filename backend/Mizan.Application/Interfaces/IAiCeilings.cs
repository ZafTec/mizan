namespace Mizan.Application.Interfaces;

/// <summary>
/// The global ceilings as they stand now, configuration or a runtime setting, exposed so the admin
/// view can show usage against them without Application taking a dependency on Infrastructure's
/// options type.
/// </summary>
public interface IAiCeilings
{
    Task<AiGlobalCeilings> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record AiGlobalCeilings(long GlobalDailyTokens, long GlobalDailyCostMicros);
