"use server";

import { callBackendApi } from "@/lib/backend-api-client";
import { createErrorState, createSuccessState, FormState } from "@/helper/FormErrorHandler";

/**
 * Add a meal entry via the backend API (Server Action)
 */
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

        await callBackendApi("/api/Meals", {
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
            },
        });

        return createSuccessState("Meal logged successfully!");
    } catch (error) {
        console.error("Failed to add meal:", error);
        return createErrorState("Failed to log meal");
    }
}

/**
 * Delete a meal entry
 */
export async function deleteMeal(id: string): Promise<{ success: boolean; message?: string }> {
    try {
        await callBackendApi(`/api/Meals/${id}`, {
            method: "DELETE",
        });
        return { success: true };
    } catch (error) {
        console.error("Failed to delete meal:", error);
        return { success: false, message: "Failed to delete meal" };
    }
}
