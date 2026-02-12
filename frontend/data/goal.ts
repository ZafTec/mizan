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
    targetWeight?: number;
    weightUnit?: string;
    targetDate?: string;
    isActive: boolean;
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
 * Create or update user goal
 */
export async function createGoal(prevState: FormState, formData: FormData): Promise<FormState> {
    try {
        const goalType = formData.get("goalType") as string || "maintenance";
        const targetCalories = parseInt(formData.get("calories") as string);
        const targetProtein = parseFloat(formData.get("protein") as string);
        const targetCarbs = parseFloat(formData.get("carbs") as string);
        const targetFat = parseFloat(formData.get("fat") as string);

        if (isNaN(targetCalories)) {
            return createErrorState("Target calories are required", [
                { field: "calories", message: "Valid calories required" }
            ]);
        }

        await serverApi("/api/Goals", {
            method: "POST",
            body: {
                goalType,
                targetCalories: isNaN(targetCalories) ? null : targetCalories,
                targetProteinGrams: isNaN(targetProtein) ? null : targetProtein,
                targetCarbsGrams: isNaN(targetCarbs) ? null : targetCarbs,
                targetFatGrams: isNaN(targetFat) ? null : targetFat,
            },
        });

        return createSuccessState("Goal saved successfully!");
    } catch (error) {
        goalLogger.error("Failed to save goal", { error });
        return createErrorState("Failed to save goal");
    }
}
