namespace Mizan.Domain.Achievements;

/// <summary>
/// The criteria vocabulary. Constants because the seed data, the evaluator and
/// the progress bars all name these, and a typo produces an achievement that
/// silently never unlocks.
/// </summary>
public static class CriteriaTypes
{
    public const string MealsLogged = "meals_logged";
    public const string RecipesCreated = "recipes_created";
    public const string WorkoutsLogged = "workouts_logged";
    public const string BodyMeasurementsLogged = "body_measurements_logged";
    public const string GoalProgressLogged = "goal_progress_logged";
    public const string StreakNutrition = "streak_nutrition";
    public const string StreakWorkout = "streak_workout";
    public const string PointsTotal = "points_total";
    public const string TotalVolumeKg = "total_volume_kg";
    public const string TemplateCompletedCount = "template_completed_count";
    public const string FollowersCount = "followers_count";
    public const string WorkoutsShared = "workouts_shared";
    public const string ReactionsGiven = "reactions_given";
    public const string CommentsMade = "comments_made";
    public const string PrCount = "pr_count";

    /// <summary>
    /// The five backed by <c>user_activity_counters</c> rather than a
    /// <c>COUNT(*)</c> over the user's whole history.
    /// </summary>
    public static readonly IReadOnlySet<string> Counted = new HashSet<string>
    {
        MealsLogged, RecipesCreated, WorkoutsLogged, BodyMeasurementsLogged, GoalProgressLogged,
    };
}

/// <summary>
/// Everything an achievement can be measured against, in one shape.
///
/// There used to be two of these - a class in the evaluator and a dictionary
/// in the query - computed by two near-identical methods. They drifted:
/// the query's streaks were raw stored counts while the evaluator's were too,
/// and neither applied the decay rule, so a lapsed 30-day streak still
/// unlocked a 30-day badge.
/// </summary>
public sealed record UserActivityStats
{
    public int MealsLogged { get; init; }
    public int RecipesCreated { get; init; }
    public int WorkoutsLogged { get; init; }
    public int BodyMeasurementsLogged { get; init; }
    public int GoalProgressLogged { get; init; }
    public decimal TotalVolumeKg { get; init; }
    public int TemplateCompletedCount { get; init; }
    public int FollowersCount { get; init; }
    public int WorkoutsShared { get; init; }
    public int ReactionsGiven { get; init; }
    public int CommentsMade { get; init; }
    public int PrCount { get; init; }

    /// <summary>Live streak length, after decay - never the stored count.</summary>
    public int StreakNutrition { get; init; }

    public int StreakWorkout { get; init; }
    public int EarnedPoints { get; init; }

    public static UserActivityStats Empty { get; } = new();

    /// <summary>The user's progress toward a criteria type, for a progress bar.</summary>
    public decimal Value(string? criteriaType) => criteriaType switch
    {
        CriteriaTypes.MealsLogged => MealsLogged,
        CriteriaTypes.RecipesCreated => RecipesCreated,
        CriteriaTypes.WorkoutsLogged => WorkoutsLogged,
        CriteriaTypes.BodyMeasurementsLogged => BodyMeasurementsLogged,
        CriteriaTypes.GoalProgressLogged => GoalProgressLogged,
        CriteriaTypes.StreakNutrition => StreakNutrition,
        CriteriaTypes.StreakWorkout => StreakWorkout,
        CriteriaTypes.PointsTotal => EarnedPoints,
        CriteriaTypes.TotalVolumeKg => TotalVolumeKg,
        CriteriaTypes.TemplateCompletedCount => TemplateCompletedCount,
        CriteriaTypes.FollowersCount => FollowersCount,
        CriteriaTypes.WorkoutsShared => WorkoutsShared,
        CriteriaTypes.ReactionsGiven => ReactionsGiven,
        CriteriaTypes.CommentsMade => CommentsMade,
        CriteriaTypes.PrCount => PrCount,
        _ => 0,
    };

    /// <summary>
    /// An unknown criteria type never unlocks. Deliberate: a typo in seed data
    /// should award nothing rather than award everything.
    /// </summary>
    public bool Meets(string? criteriaType, decimal threshold) =>
        criteriaType is not null
        && Value(criteriaType) >= threshold
        && Value(criteriaType) > 0;
}
