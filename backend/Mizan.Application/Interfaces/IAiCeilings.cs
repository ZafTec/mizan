namespace Mizan.Application.Interfaces;

/// <summary>
/// The configured global ceilings, exposed so the admin view can show usage
/// against them without Application taking a dependency on Infrastructure's
/// options type.
/// </summary>
public interface IAiCeilings
{
    long GlobalDailyTokens { get; }
    long GlobalDailyCostMicros { get; }
}
