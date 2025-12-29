"use server";
import { logger } from "@/lib/logger";

const mealPlanLogger = logger.createModuleLogger("meal-plan-data");

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
 * TODO: Meal plans from the backend API
 */


export async function getWeeklyMealPlans(startDate: string, endDate: string): Promise<WeeklyMealPlan[]> {
    try {
        mealPlanLogger.info("getWeeklyMealPlans called, but endpoint not yet implemented in backend", { startDate, endDate });
        return [];
    } catch (error) {
        mealPlanLogger.error("Failed to get weekly meal plans", { error, startDate, endDate });
        return [];
    }
}

export async function deleteMealPlan(mealPlanId: string): Promise<boolean> {
    try {
        mealPlanLogger.info("deleteMealPlan called, but endpoint not yet implemented in backend", { mealPlanId });
        return false;
    } catch (error) {
        mealPlanLogger.error("Failed to delete meal plan", { error, mealPlanId });
        return false;
    }
}
