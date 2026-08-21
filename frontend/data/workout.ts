"use server";

import { serverApi } from "@/lib/api.server";
import { ApiError } from "@/lib/api";
import { logger } from "@/lib/logger";
import { parseStoredDraft } from "@/lib/workouts/draft";

const workoutLogger = logger.createModuleLogger("workout-data");

export interface WorkoutExercise {
	exerciseId: string;
	sets: number;
	reps: number;
	weightKg?: number;
	durationSeconds?: number;
	notes?: string;
}

export interface LogWorkoutData {
	name: string;
	date: string;
	durationMinutes: number;
	caloriesBurned?: number;
	exercises: WorkoutExercise[];
}

export interface WorkoutSetSummary {
	setNumber: number;
	reps: number | null;
	weightKg: number | null;
	durationSeconds: number | null;
	distanceMeters: number | null;
	completed: boolean;
}

export interface WorkoutExerciseSummary {
	id: string;
	exerciseName: string;
	category: string;
	muscleGroup: string | null;
	sortOrder: number;
	sets: WorkoutSetSummary[];
}

export interface WorkoutSummary {
	id: string;
	name: string | null;
	workoutDate: string;
	durationMinutes: number | null;
	caloriesBurned: number | null;
	notes: string | null;
	createdAt: string;
	exercises: WorkoutExerciseSummary[];
}

export interface WorkoutHistoryResult {
	items: WorkoutSummary[];
	totalCount: number;
	page: number;
	pageSize: number;
	totalPages: number;
}

export async function getWorkouts(
	page = 1,
	pageSize = 10,
	sortBy?: string,
	sortOrder?: string,
): Promise<WorkoutHistoryResult> {
	try {
		const params = new URLSearchParams({
			page: String(page),
			pageSize: String(pageSize),
		});
		if (sortBy) params.set("sortBy", sortBy);
		if (sortOrder) params.set("sortOrder", sortOrder);

		return await serverApi<WorkoutHistoryResult>(`/api/Workouts?${params}`);
	} catch (error) {
		workoutLogger.error("Failed to fetch workouts", { error });
		return { items: [], totalCount: 0, page, pageSize, totalPages: 0 };
	}
}

export async function logWorkout(data: LogWorkoutData): Promise<boolean> {
	try {
		await serverApi("/api/Workouts", {
			method: "POST",
			body: data,
		});
		return true;
	} catch (error) {
		workoutLogger.error("Failed to log workout", { error });
		return false;
	}
}

export interface ActiveWorkoutDraft {
	name: string;
	exerciseCount: number;
	completedSets: number;
	totalSets: number;
	startedAt: string;
	updatedAt: string;
}

/**
 * The open session, if there is one. Null is the normal answer - the API
 * returns 404 when nothing is in progress - so callers render nothing rather
 * than an empty state.
 */
export async function getActiveWorkoutDraft(): Promise<ActiveWorkoutDraft | null> {
	let raw: { payload: string; updatedAt: string };
	try {
		raw = await serverApi<{ payload: string; updatedAt: string }>("/api/Workouts/draft");
	} catch (error) {
		if (error instanceof ApiError && error.status === 404) return null;
		workoutLogger.error("Failed to load workout draft", { error });
		return null;
	}

	const stored = parseStoredDraft(raw.payload, raw.updatedAt);
	if (!stored) {
		workoutLogger.warn("Discarding unparseable workout draft");
		return null;
	}

	const sets = stored.draft.exercises.flatMap((e) => e.sets);
	return {
		name: stored.draft.name || "Workout",
		exerciseCount: stored.draft.exercises.length,
		completedSets: sets.filter((s) => s.completedAt).length,
		totalSets: sets.length,
		startedAt: stored.draft.startedAt,
		updatedAt: stored.updatedAt,
	};
}
