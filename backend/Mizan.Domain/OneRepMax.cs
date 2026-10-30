namespace Mizan.Domain;

/// <summary>An estimated one-rep max from one set, by the Epley formula.</summary>
public static class OneRepMax
{
    /// <summary>
    /// Weight × (1 + reps / 30), to 0.1 kg, the precision a plate can be loaded to and the
    /// app shows. A set without weight estimates nothing.
    /// </summary>
    public static decimal Estimate(decimal weightKg, int reps) =>
        weightKg <= 0 ? 0 : Math.Round(weightKg * (1 + Math.Max(0, reps) / 30m), 1, MidpointRounding.AwayFromZero);
}
