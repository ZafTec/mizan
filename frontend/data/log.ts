import "server-only";
import { serverApi } from "@/lib/api.server";
import { ApiError } from "@/lib/api";
import type { DailyNutritionSummary } from "@/data/meal";
import type { components } from "@/types/api.generated";
import type { BodyMeasurement } from "@/data/bodyMeasurement";
import type { WorkoutSummary } from "@/data/workout";
import type { UserGoal } from "@/data/goal";

/** A diary row and the day as GET /api/Meals sends them, with the server's meal and recipe subtotals. */
export type LogMeal = components["schemas"]["FoodDiaryEntryDto"];
export type DayDiary = components["schemas"]["FoodDiaryResult"];
export type LogWorkout = WorkoutSummary & {
  exercises: (WorkoutSummary["exercises"][number] & { exerciseId: string })[];
};

type Page<T> = { items: T[]; totalPages: number };

/** Follow all pages in the selected range. A failed page must not become an empty log. */
async function rangeItems<T>(
  path: string,
  from: string,
  to: string,
): Promise<T[]> {
  const params = new URLSearchParams({
    from,
    to,
    pageSize: "100",
    sortBy: "date",
    sortOrder: "desc",
  });
  const first = await serverApi<Page<T>>(`${path}?${params}&page=1`);
  const items = [...first.items];
  for (let page = 2; page <= first.totalPages; page++) {
    items.push(
      ...(await serverApi<Page<T>>(`${path}?${params}&page=${page}`)).items,
    );
  }
  return items;
}

export function getLogWorkouts(from: string, to: string) {
  return rangeItems<LogWorkout>("/api/Workouts", from, to);
}

export function getLogMeasurements(from: string, to: string) {
  return rangeItems<BodyMeasurement>("/api/BodyMeasurements", from, to);
}

export function getDayDiary(date: string) {
  return serverApi<DayDiary>(`/api/Meals?date=${date}`);
}

export async function getLogNutrition(days: number, endDate: string) {
  return (
    await serverApi<{ days: DailyNutritionSummary[] }>(
      `/api/Meals/range?days=${days}&endDate=${endDate}`,
    )
  ).days;
}

export async function getLogGoal(): Promise<UserGoal | null> {
  try {
    return await serverApi<UserGoal | null>("/api/Goals", {
      expectedStatuses: [404],
    });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}
