using System.Globalization;

namespace Mizan.Domain.Goals;

/// <summary>A body target a goal can set and a measurement can reach.</summary>
public enum GoalMetric
{
    Weight,
    BodyFat,
    MuscleMass,
}

/// <summary>A goal's body targets, weight in kilograms whatever unit the goal was set in.</summary>
public sealed record BodyTargets(decimal? WeightKg, decimal? BodyFatPercentage, decimal? MuscleMassKg);

/// <summary>Body values: one measurement, or the latest earlier value of each metric.</summary>
public sealed record BodyReading(decimal? WeightKg, decimal? BodyFatPercentage, decimal? MuscleMassKg);

/// <summary>
/// When a measurement reaches a goal's target (docs/ARCHITECTURE.md#notifications). A target is
/// reached when the value before was on one side of it and the new one is on it or past it, in
/// whichever direction the person was heading. Without an earlier value there is no direction, so
/// nothing is reached; a value that already sat on the target was reached before.
/// </summary>
public static class GoalReached
{
    public const decimal KilogramsPerPound = 0.45359237m;

    public static decimal? TargetWeightKg(decimal? target, string? unit) =>
        target is { } value ? (IsPounds(unit) ? value * KilogramsPerPound : value) : null;

    public static IReadOnlyList<GoalMetric> Crossed(BodyTargets targets, BodyReading previous, BodyReading latest)
    {
        var reached = new List<GoalMetric>();
        if (Crossed(targets.WeightKg, previous.WeightKg, latest.WeightKg)) reached.Add(GoalMetric.Weight);
        if (Crossed(targets.BodyFatPercentage, previous.BodyFatPercentage, latest.BodyFatPercentage)) reached.Add(GoalMetric.BodyFat);
        if (Crossed(targets.MuscleMassKg, previous.MuscleMassKg, latest.MuscleMassKg)) reached.Add(GoalMetric.MuscleMass);
        return reached;
    }

    public static bool Crossed(decimal? target, decimal? previous, decimal? latest) =>
        target is { } t && previous is { } before && latest is { } now
        && ((before > t && now <= t) || (before < t && now >= t));

    /// <summary>A weight in the goal's unit, such as "72 kg" or "158.7 lb".</summary>
    public static string Weight(decimal kilograms, string? unit) =>
        IsPounds(unit) ? $"{Number(kilograms / KilogramsPerPound)} lb" : $"{Number(kilograms)} kg";

    public static string Number(decimal value) =>
        Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);

    private static bool IsPounds(string? unit) => string.Equals(unit, "lb", StringComparison.OrdinalIgnoreCase);
}
