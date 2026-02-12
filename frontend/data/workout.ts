"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";

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
