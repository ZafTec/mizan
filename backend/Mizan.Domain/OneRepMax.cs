namespace Mizan.Domain;

/// <summary>An estimated one-rep max, by the Epley formula.</summary>
public static class OneRepMax
{
    /// <summary>
    /// Weight × (1 + reps / 30), to 0.1 kg, the precision a plate can be loaded to and the
    /// app shows. A set without weight estimates nothing.
    /// </summary>
    public static decimal Estimate(decimal weightKg, int reps) =>
        weightKg <= 0 ? 0 : Math.Round(weightKg * (1 + Math.Max(0, reps) / 30m), 1, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The best estimate over a session's sets: the largest Epley value, whatever order the sets
    /// were logged or read in. No weighted set estimates nothing.
    /// </summary>
    public static decimal Best(IEnumerable<(decimal? WeightKg, int? Reps)> sets) =>
        sets.Select(set => Estimate(set.WeightKg ?? 0, set.Reps ?? 0)).DefaultIfEmpty(0).Max();
}
