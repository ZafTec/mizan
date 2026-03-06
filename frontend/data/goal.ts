"use server";

import { serverApi } from "@/lib/api.server";
import { createErrorState, createSuccessState, FormState } from "@/helper/FormErrorHandler";
import { logger } from "@/lib/logger";
const goalLogger = logger.createModuleLogger("goal-data");

export interface UserGoal {
    id: string;
    goalType?: string;
    targetCalories?: number;
    targetProteinGrams?: number;
    targetCarbsGrams?: number;
    targetFatGrams?: number;
    targetFiberGrams?: number | null;
    targetWeight?: number;
    weightUnit?: string;
    targetBodyFatPercentage?: number | null;
    targetMuscleMassKg?: number | null;
    targetProteinCalorieRatio?: number | null;
    targetDate?: string;
    isActive: boolean;
    createdAt: string;
}

/**
 * Get the current user's active goal
 */
export async function getCurrentGoal(): Promise<UserGoal | null> {
    try {
        const result = await serverApi<UserGoal>("/api/Goals");
        return result;
    } catch (error) {
        goalLogger.error("Failed to get current goal", { error });
        return null;
    }
}

/**
 * Get full goal history (active + inactive) ordered by creation date
 */
export async function getGoalHistory(): Promise<UserGoal[]> {
    try {
        const result = await serverApi<UserGoal[]>("/api/Goals/history");
        return result || [];
    } catch (error) {
        goalLogger.error("Failed to get goal history", { error });
        return [];
    }
}

/**
 * Create or update user goal
 */
export async function createGoal(prevState: FormState, formData: FormData): Promise<FormState> {
    try {
        const goalType = formData.get("goalType") as string || "maintenance";
        const targetCalories = parseInt(formData.get("calories") as string);
        const targetProtein = parseFloat(formData.get("protein") as string);
        const targetCarbs = parseFloat(formData.get("carbs") as string);
        const targetFat = parseFloat(formData.get("fat") as string);
        const targetFiber = parseFloat(formData.get("fiber") as string);
        const targetBodyFat = parseFloat(formData.get("targetBodyFatPercentage") as string);
        const targetMuscle = parseFloat(formData.get("targetMuscleMassKg") as string);
        const targetPcal = parseFloat(formData.get("targetProteinCalorieRatio") as string);
        const targetWeight = parseFloat(formData.get("targetWeight") as string);
        const weightUnit = (formData.get("weightUnit") as string) || "kg";
        const targetDate = formData.get("targetDate") as string;

        if (isNaN(targetCalories)) {
            return createErrorState("Target calories are required", [
                { field: "calories", message: "Valid calories required" }
            ]);
        }

        const result = await serverApi<{ id: string; success: boolean; message?: string; warnings?: string[] }>("/api/Goals", {
            method: "POST",
            body: {
                goalType,
                targetCalories: isNaN(targetCalories) ? null : targetCalories,
                targetProteinGrams: isNaN(targetProtein) ? null : targetProtein,
                targetCarbsGrams: isNaN(targetCarbs) ? null : targetCarbs,
                targetFatGrams: isNaN(targetFat) ? null : targetFat,
                targetFiberGrams: isNaN(targetFiber) ? null : targetFiber,
                targetBodyFatPercentage: isNaN(targetBodyFat) ? null : targetBodyFat,
                targetMuscleMassKg: isNaN(targetMuscle) ? null : targetMuscle,
                targetProteinCalorieRatio: isNaN(targetPcal) ? null : targetPcal,
                targetWeight: isNaN(targetWeight) ? null : targetWeight,
                weightUnit: weightUnit || "kg",
                targetDate: targetDate || null,
            },
        });

        return {
            status: "success",
            message: "Goal saved successfully!",
            warnings: result?.warnings?.length ? result.warnings : undefined,
        };
    } catch (error) {
        goalLogger.error("Failed to save goal", { error });
        return createErrorState("Failed to save goal");
    }
}
