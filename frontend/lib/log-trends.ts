import type { LogWorkout } from "@/data/log";
import type { BodyMeasurement } from "@/data/bodyMeasurement";

/** Range results are newest-first, including multiple measurements on one day. */
export function dailyWeightTrend(measurements: BodyMeasurement[]) {
  const latest = new Map<string, number>();
  for (const measurement of measurements) {
    const date = measurement.date.slice(0, 10);
    if (measurement.weightKg != null && !latest.has(date))
      latest.set(date, measurement.weightKg);
  }
  return [...latest]
    .map(([date, weight]) => ({ date, weight }))
    .sort((a, b) => a.date.localeCompare(b.date));
}

/** One point per exercise/day; only completed working sets contribute. */
export function exerciseTrends(workouts: LogWorkout[]) {
  const exercises = new Map<
    string,
    {
      id: string;
      name: string;
      days: Map<
        string,
        { date: string; volume: number; estimatedMax: number | null }
      >;
    }
  >();
  for (const workout of workouts) {
    for (const exercise of workout.exercises) {
      const sets = exercise.sets.filter(
        (set) =>
          set.completed && (set.reps ?? 0) > 0 && (set.weightKg ?? 0) > 0,
      );
      if (!sets.length) continue;
      const id = exercise.exerciseId;
      const trend = exercises.get(id) ?? {
        id,
        name: exercise.exerciseName,
        days: new Map(),
      };
      const date = workout.workoutDate.slice(0, 10);
      const point = trend.days.get(date) ?? {
        date,
        volume: 0,
        estimatedMax: null,
      };
      for (const set of sets) {
        const reps = set.reps!;
        const weight = set.weightKg!;
        point.volume += reps * weight;
        // High-rep sets make a poor maximum estimate. Keep their volume only.
        if (reps <= 12)
          point.estimatedMax = Math.max(
            point.estimatedMax ?? 0,
            reps === 1 ? weight : weight * (1 + reps / 30),
          );
      }
      trend.days.set(date, point);
      exercises.set(id, trend);
    }
  }
  return [...exercises.values()]
    .map(({ id, name, days }) => ({
      id,
      name,
      points: [...days.values()]
        .sort((a, b) => a.date.localeCompare(b.date))
        .map((point) => ({
          ...point,
          volume: Math.round(point.volume),
          estimatedMax:
            point.estimatedMax == null
              ? null
              : Math.round(point.estimatedMax * 10) / 10,
        })),
    }))
    .sort((a, b) => a.name.localeCompare(b.name));
}
