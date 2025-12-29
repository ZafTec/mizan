"use server";

import { callBackendApi } from "@/lib/backend-api-client";
import { logger } from "@/lib/logger";

const mealLogger = logger.createModuleLogger("meal-data");

export interface MealEntry {
    id: string;
    foodId?: string;
    recipeId?: string;
    name: string;
    mealType: string;
    servings: number;
    calories?: number;
    proteinGrams?: number;
    carbsGrams?: number;
    fatGrams?: number;
    loggedAt: string;
}

export interface FoodDiaryResult {
    date: string;
    entries: MealEntry[];
    totals: {
        calories: number;
        protein: number;
        carbs: number;
        fat: number;
    };
}

/**
 * Get today's meals from the backend API
 */
export async function getTodayMeal(): Promise<MealEntry[]> {
    try {
        const today = new Date().toISOString().split("T")[0];
        const result = await callBackendApi<FoodDiaryResult>(`/api/Meals?date=${today}`);
        return result.entries || [];
    } catch (error) {
        mealLogger.error("Failed to get today's meals", { error });
        return [];
    }
}

/**
 * Get meals for a specific date
 */
export async function getMeal(date: string): Promise<MealEntry[]> {
    try {
        const result = await callBackendApi<FoodDiaryResult>(`/api/Meals?date=${date}`);
        return result.entries || [];
    } catch (error) {
        mealLogger.error("Failed to get meals for date", { error, date });
        return [];
    }
}

/**
 * Get daily totals
 */
export async function getDailyTotals(date?: string): Promise<FoodDiaryResult["totals"] | null> {
    try {
        const queryDate = date || new Date().toISOString().split("T")[0];
        const result = await callBackendApi<FoodDiaryResult>(`/api/Meals?date=${queryDate}`);
        return result.totals;
    } catch (error) {
        mealLogger.error("Failed to get daily totals", { error, date: date || "today" });
        return null;
    }
}
