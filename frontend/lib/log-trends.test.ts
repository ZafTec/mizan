import { describe, expect, it } from "vitest";
import type { LogWorkout } from "@/data/log";
import { dailyWeightTrend, exerciseTrends } from "./log-trends";

function workout(
  date: string,
  sets: { completed: boolean; reps: number; weightKg: number }[],
): LogWorkout {
  return {
    id: date,
    name: "Training",
    workoutDate: date,
    durationMinutes: 30,
    caloriesBurned: null,
    notes: null,
    createdAt: date,
    exercises: [
      {
        id: date,
        exerciseId: "squat",
        exerciseName: "Squat",
        category: "strength",
        muscleGroup: "legs",
        sortOrder: 0,
        sets: sets.map((set, index) => ({
          ...set,
          setNumber: index + 1,
          durationSeconds: null,
          distanceMeters: null,
        })),
      },
    ],
  };
}

describe("exercise trends", () => {
  it("uses the latest weight per day without reversing the direction of change", () => {
    expect(
      dailyWeightTrend([
        { id: "latest", date: "2026-09-07T00:00:00", weightKg: 72 },
        { id: "earlier", date: "2026-09-07T00:00:00", weightKg: 73 },
        { id: "previous", date: "2026-09-06T00:00:00", weightKg: 73 },
      ]),
    ).toEqual([
      { date: "2026-09-06", weight: 73 },
      { date: "2026-09-07", weight: 72 },
    ]);
  });
  it("aggregates sessions on a day and excludes unfinished sets", () => {
    const trends = exerciseTrends([
      workout("2026-09-07", [
        { reps: 5, weightKg: 60, completed: true },
        { reps: 5, weightKg: 500, completed: false },
      ]),
      workout("2026-09-07", [{ reps: 5, weightKg: 60, completed: true }]),
      workout("2026-09-01", [{ reps: 1, weightKg: 65, completed: true }]),
    ]);
    expect(trends[0].points).toEqual([
      { date: "2026-09-01", volume: 65, estimatedMax: 65 },
      { date: "2026-09-07", volume: 600, estimatedMax: 70 },
    ]);
  });
  it("keeps high-rep volume without claiming a max estimate", () => {
    expect(
      exerciseTrends([
        workout("2026-09-07", [{ reps: 20, weightKg: 25, completed: true }]),
      ])[0].points[0],
    ).toEqual({ date: "2026-09-07", volume: 500, estimatedMax: null });
    expect(
      exerciseTrends([
        workout("2026-09-07", [{ reps: 5, weightKg: 0, completed: true }]),
      ]),
    ).toEqual([]);
  });
});
