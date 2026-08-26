using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Data;

/// <summary>
/// One upsert, arithmetic done by Postgres.
///
/// EF cannot express <c>ON CONFLICT DO UPDATE SET n = n + 1</c>, and the EF
/// way - load, add one, save - is both an extra round trip and a lost update
/// whenever two logs land together. This is the case where raw SQL is the
/// simple option rather than the clever one.
/// </summary>
public static class ActivityCounterSql
{
    public static string Adjust(ActivityCounter counter)
    {
        var column = Column(counter);
        return
            $"INSERT INTO user_activity_counters (user_id, {column}, updated_at) " +
            "VALUES ({0}, GREATEST({1}, 0), NOW()) " +
            "ON CONFLICT (user_id) DO UPDATE SET " +
            $"{column} = GREATEST(user_activity_counters.{column} + {{1}}, 0), updated_at = NOW();";
    }

    /// <summary>
    /// Interpolated into SQL, so it must never come from a caller. The enum is
    /// the guarantee: an unmapped value throws rather than reaching Postgres.
    /// </summary>
    private static string Column(ActivityCounter counter) => counter switch
    {
        ActivityCounter.Meals => "meals_logged",
        ActivityCounter.Recipes => "recipes_created",
        ActivityCounter.Workouts => "workouts_logged",
        ActivityCounter.BodyMeasurements => "body_measurements_logged",
        ActivityCounter.GoalProgress => "goal_progress_logged",
        _ => throw new ArgumentOutOfRangeException(nameof(counter), counter, "Unmapped activity counter"),
    };
}
