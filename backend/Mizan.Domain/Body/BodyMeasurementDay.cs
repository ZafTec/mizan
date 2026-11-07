using Mizan.Domain.Entities;

namespace Mizan.Domain.Body;

/// <summary>The values one write carries. A null value is not part of the write.</summary>
public sealed record BodyMeasurementValues(
    decimal? WeightKg = null,
    decimal? BodyFatPercentage = null,
    decimal? MuscleMassKg = null,
    decimal? WaistCm = null,
    decimal? HipsCm = null,
    decimal? ChestCm = null,
    decimal? LeftArmCm = null,
    decimal? RightArmCm = null,
    decimal? LeftThighCm = null,
    decimal? RightThighCm = null);

/// <summary>
/// A person has one body measurement a day. A later write that day updates it: each value it carries
/// replaces the stored one, and a value it leaves out stays. So a weight from a workout and a weight
/// from the day's log are one weight, the later one.
/// </summary>
public static class BodyMeasurementDay
{
    public static void Apply(BodyMeasurement day, BodyMeasurementValues values, string? notes)
    {
        day.WeightKg = values.WeightKg ?? day.WeightKg;
        day.BodyFatPercentage = values.BodyFatPercentage ?? day.BodyFatPercentage;
        day.MuscleMassKg = values.MuscleMassKg ?? day.MuscleMassKg;
        day.WaistCm = values.WaistCm ?? day.WaistCm;
        day.HipsCm = values.HipsCm ?? day.HipsCm;
        day.ChestCm = values.ChestCm ?? day.ChestCm;
        day.LeftArmCm = values.LeftArmCm ?? day.LeftArmCm;
        day.RightArmCm = values.RightArmCm ?? day.RightArmCm;
        day.LeftThighCm = values.LeftThighCm ?? day.LeftThighCm;
        day.RightThighCm = values.RightThighCm ?? day.RightThighCm;
        if (!string.IsNullOrWhiteSpace(notes)) day.Notes = notes;
    }
}
