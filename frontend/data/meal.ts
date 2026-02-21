"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import { createErrorState, createSuccessState, FormState } from "@/helper/FormErrorHandler";

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
    fiberGrams?: number;
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
        fiber: number;
    };
}

/**
 * Get today's meals from the backend API
 */
export async function getTodayMeal(): Promise<MealEntry[]> {
    try {
        const today = new Date().toISOString().split("T")[0];
        const result = await serverApi<FoodDiaryResult>(`/api/Meals?date=${today}`);
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
        const result = await serverApi<FoodDiaryResult>(`/api/Meals?date=${date}`);
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
        const result = await serverApi<FoodDiaryResult>(`/api/Meals?date=${queryDate}`);
        return result.totals;
    } catch (error) {
        mealLogger.error("Failed to get daily totals", { error, date: date || "today" });
        return null;
    }
}

export async function addMeal(prevState: FormState, formData: FormData): Promise<FormState> {
    try {
        const recipeId = formData.get("recipeId") as string;
        const foodId = formData.get("foodId") as string;
        const mealType = ((formData.get("mealType") as string) || "SNACK").toUpperCase();
        const servings = parseFloat(formData.get("servings") as string) || 1;
        const date = (formData.get("date") as string) || new Date().toISOString().split("T")[0];
        const calories = parseInt(formData.get("calories") as string);
        const protein = parseFloat(formData.get("protein") as string);
        const carbs = parseFloat(formData.get("carbs") as string);
        const fat = parseFloat(formData.get("fat") as string);
        const fiber = parseFloat(formData.get("fiber") as string);

        await serverApi("/api/Meals", {
            method: "POST",
            body: {
                recipeId: recipeId || null,
                foodId: foodId || null,
                entryDate: date,
                mealType,
                servings,
                name: (formData.get("name") as string) || "Unknown",
                calories: isNaN(calories) ? null : calories,
                proteinGrams: isNaN(protein) ? null : protein,
                carbsGrams: isNaN(carbs) ? null : carbs,
                fatGrams: isNaN(fat) ? null : fat,
                fiberGrams: isNaN(fiber) ? null : fiber,
            },
        });

        mealLogger.info("Meal logged successfully", { mealType, servings, date });
        return createSuccessState("Meal logged successfully!");
    } catch (error) {
        mealLogger.error("Failed to log meal", {
            error: error instanceof Error ? error.message : String(error),
        });
        return createErrorState("Failed to log meal.");
    }
}

export async function deleteMeal(id: string): Promise<{ success: boolean; message?: string }> {
    try {
        await serverApi(`/api/Meals/${id}`, { method: "DELETE" });
        return { success: true };
    } catch (error) {
        mealLogger.error("Failed to delete meal", {
            error: error instanceof Error ? error.message : String(error),
            mealID: id,
        });
        return { success: false, message: "Failed to delete meal" };
    }
}
