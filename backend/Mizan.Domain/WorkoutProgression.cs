namespace Mizan.Domain;

public static class WorkoutProgression
{
    public static IReadOnlyList<decimal> Apply(
        IReadOnlyList<decimal> weights,
        string progressionType,
        decimal amount,
        string strategy = "All")
    {
        if (weights.Count == 0 || amount == 0 || string.Equals(progressionType, "None", StringComparison.OrdinalIgnoreCase))
        {
            return weights.ToArray();
        }

        if (string.Equals(progressionType, "IncreaseAllEvenly", StringComparison.OrdinalIgnoreCase))
        {
            return weights.Select(weight => weight + amount).ToArray();
        }

        if (!string.Equals(progressionType, "IncreaseLowestSet", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(nameof(progressionType));
        }

        var result = weights.ToArray();
        var minimum = result.Min();
        var matches = result.Select((weight, index) => (weight, index)).Where(x => x.weight == minimum).Select(x => x.index).ToArray();
        var selected = strategy.ToLowerInvariant() switch
        {
            "all" => matches,
            "first" => [matches.First()],
            "last" => [matches.Last()],
            "middle" => [matches.OrderBy(index => Math.Abs(index - ((result.Length - 1) / 2d))).First()],
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };
        foreach (var index in selected) result[index] += amount;
        return result;
    }
}
