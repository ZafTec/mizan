namespace Mizan.Contracts.Measurements;

/// <summary>
/// Body of POST /api/BodyMeasurements. Every field is optional: a weigh-in is
/// usually one number, and forcing a full body scan to record it would stop
/// people logging at all.
/// </summary>
public record LogMeasurementRequest(
    DateTime? Date,
    decimal? WeightKg,
    decimal? BodyFatPercentage,
    decimal? MuscleMassKg,
    decimal? WaistCm,
    decimal? HipsCm,
    decimal? ChestCm,
    decimal? LeftArmCm,
    decimal? RightArmCm,
    decimal? LeftThighCm,
    decimal? RightThighCm,
    string? Notes
);
