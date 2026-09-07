import "server-only";
import { serverApi } from "@/lib/api.server";
import { ApiError } from "@/lib/api";
import type {
  MealEntry,
  FoodDiaryResult,
  DailyNutritionSummary,
} from "@/data/meal";
import type { BodyMeasurement } from "@/data/bodyMeasurement";
import type { WorkoutSummary } from "@/data/workout";
import type { UserGoal } from "@/data/goal";

export type LogMeal = MealEntry & {
  groupId?: string | null;
  groupName?: string | null;
  amountGrams?: number | null;
};
export type DayDiary = Omit<FoodDiaryResult, "entries"> & {
  entries: LogMeal[];
};
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
