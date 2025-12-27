"use server";

import { callBackendApi } from "@/lib/backend-api-client";

export interface MealPlanEntry {
    id: string;
    recipeId: string;
    recipeTitle: string;
    date: string;
    mealType: string;
    servings: number;
}

export interface WeeklyMealPlan {
    id: string;
    name: string;
    startDate: string;
    endDate: string;
    meals: MealPlanEntry[];
}

/**
 * Get weekly meal plans from the backend API
 * Note: This may need a dedicated endpoint on the backend
 */
export async function getWeeklyMealPlans(startDate: string, endDate: string): Promise<WeeklyMealPlan[]> {
    try {
        // Placeholder - the backend may need a meal plan endpoint
        // For now, return empty array until backend endpoint is available
        console.log("Meal plans endpoint not yet implemented in backend");
        return [];
    } catch (error) {
        console.error("Failed to get weekly meal plans:", error);
        return [];
    }
}

/**
 * Delete a meal plan via the backend API
 */
export async function deleteMealPlan(mealPlanId: string): Promise<boolean> {
    try {
        // Placeholder - the backend may need a meal plan endpoint
        console.log("Delete meal plan endpoint not yet implemented in backend");
        return false;
    } catch (error) {
        console.error("Failed to delete meal plan:", error);
        return false;
    }
}
