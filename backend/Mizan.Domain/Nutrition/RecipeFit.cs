namespace Mizan.Domain.Nutrition;

/// <summary>Calories and macros of one serving, or of what is left of a day's targets.</summary>
public readonly record struct Macros(decimal Calories, decimal Protein, decimal Carbs, decimal Fat);

/// <summary>A day's targets; a null or zero target is "no target" and does not count.</summary>
public readonly record struct DayTargets(decimal? Calories, decimal? Protein, decimal? Carbs, decimal? Fat);

/// <summary>One recipe in the order of how well a serving fits what is left, and what would be left after it.</summary>
public sealed record RecipeFitRank<T>(T Item, decimal Score, Macros LeftAfter);

/// <summary>
/// Which of a person's recipes best fits what is left of today's targets. For each target, one
/// serving fills part of what is left (up to all of it) and may go over it; the filled share
/// counts for the recipe and anything over counts twice against it, each as a share of the
/// target, so a gram of a large target weighs less than a gram of a small one. A higher score
/// fits better; a tie keeps the order the recipes came in. Without any target nothing is ranked.
/// </summary>
public static class RecipeFit
{
    private const decimal OverWeight = 2m;

    public static IReadOnlyList<RecipeFitRank<T>> Rank<T>(
        Macros eaten, DayTargets targets, IEnumerable<T> recipes, Func<T, Macros> perServing)
    {
        var axes = Axes(targets).ToList();
        if (axes.Count == 0) return [];

        var left = Remaining(eaten, targets);

        return recipes
            .Select((recipe, index) => (recipe, index, serving: perServing(recipe)))
            .Select(x => (x.recipe, x.index, x.serving, score: axes.Sum(axis => Score(axis.Target, axis.Of(left), axis.Of(x.serving)))))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Select(x => new RecipeFitRank<T>(x.recipe, Math.Round(x.score, 3), new Macros(
                left.Calories - x.serving.Calories, left.Protein - x.serving.Protein,
                left.Carbs - x.serving.Carbs, left.Fat - x.serving.Fat)))
            .ToList();
    }

    private static decimal Score(decimal target, decimal left, decimal serving)
    {
        var room = Math.Max(0m, left);
        var filled = Math.Min(Math.Max(0m, serving), room);
        var over = Math.Max(0m, serving - room);
        return (filled - OverWeight * over) / target;
    }

    /// <summary>What is left of each target after what was eaten; negative when over. Read only where there is a target.</summary>
    public static Macros Remaining(Macros eaten, DayTargets targets) => new(
        (targets.Calories ?? 0m) - eaten.Calories, (targets.Protein ?? 0m) - eaten.Protein,
        (targets.Carbs ?? 0m) - eaten.Carbs, (targets.Fat ?? 0m) - eaten.Fat);

    private static IEnumerable<(decimal Target, Func<Macros, decimal> Of)> Axes(DayTargets targets)
    {
        if (targets.Calories is { } kcal and > 0) yield return (kcal, m => m.Calories);
        if (targets.Protein is { } protein and > 0) yield return (protein, m => m.Protein);
        if (targets.Carbs is { } carbs and > 0) yield return (carbs, m => m.Carbs);
        if (targets.Fat is { } fat and > 0) yield return (fat, m => m.Fat);
    }
}
