namespace Mizan.Domain;

public sealed record WorkoutBestWeight(
    Guid ExerciseId,
    Guid WorkoutId,
    DateOnly WorkoutDate,
    DateTime WorkoutCreatedAt,
    decimal TopWeightKg);

public static class PersonalRecords
{
    public static int Count(IEnumerable<WorkoutBestWeight> workoutBestWeights)
        => Find(workoutBestWeights).Count;

    public static IReadOnlyList<WorkoutBestWeight> Find(IEnumerable<WorkoutBestWeight> workoutBestWeights)
    {
        var records = new List<WorkoutBestWeight>();
        foreach (var exerciseHistory in workoutBestWeights.GroupBy(item => item.ExerciseId))
        {
            decimal? priorBest = null;
            foreach (var workoutBest in exerciseHistory
                .OrderBy(item => item.WorkoutDate)
                .ThenBy(item => item.WorkoutCreatedAt)
                .ThenBy(item => item.WorkoutId))
            {
                if (!priorBest.HasValue || workoutBest.TopWeightKg > priorBest.Value)
                {
                    records.Add(workoutBest);
                    priorBest = workoutBest.TopWeightKg;
                }
            }
        }
        return records;
    }
}
