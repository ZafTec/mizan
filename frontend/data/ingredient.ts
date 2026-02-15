"use server";

import { revalidatePath } from "next/cache";
import { serverApi } from "@/lib/api.server";
import { createErrorState, createSuccessState, FormState } from "@/helper/FormErrorHandler";
import { logger } from "@/lib/logger";

const ingredientLogger = logger.createModuleLogger("ingredient-data");

export interface Ingredient {
    id: string;
    name: string;
    brand?: string;
    barcode?: string;
    servingSize: number;
    servingUnit: string;
    caloriesPer100g: number;
    proteinPer100g: number;
    carbsPer100g: number;
    fatPer100g: number;
    fiberPer100g?: number;
    isVerified: boolean;
}

/**
 * Get all ingredients (foods) from the backend API
 */
export async function getAllIngredient(
    searchTerm?: string,
    sortBy?: string,
    sortOrder?: string,
    page: number = 1,
    limit: number = 20
): Promise<{ ingredients: Ingredient[], totalCount: number, totalPages: number }> {
    try {
        const params = new URLSearchParams();
        params.set("Page", String(page));
        params.set("PageSize", String(limit));
        if (searchTerm) params.set("SearchTerm", searchTerm);
        if (sortBy) params.set("SortBy", sortBy);
        if (sortOrder) params.set("SortOrder", sortOrder);

        const result = await serverApi<{ items: Ingredient[], totalCount: number, page: number, pageSize: number, totalPages: number }>(`/api/Foods/search?${params.toString()}`);

        return {
            ingredients: result.items || [],
            totalCount: result.totalCount || 0,
            totalPages: result.totalPages || 0
        };
    } catch (error) {
        ingredientLogger.error("Failed to get all ingredients", { error });
        return { ingredients: [], totalCount: 0, totalPages: 0 };
    }
}

/**
 * Get ingredient by ID from the backend API
 */
export async function getIngredientById(id: string): Promise<Ingredient | null> {
    try {
        const result = await serverApi<Ingredient>(`/api/Foods/${id}`);
        return result;
    } catch (error) {
        ingredientLogger.error("Failed to get ingredient by ID", { error, ingredientId: id });
        return null;
    }
}

/**
 * Add a new ingredient with plain object data
 */
export async function addIngredientData(data: {
    name: string;
    brand?: string;
    barcode?: string;
    servingSize: number;
    calories: number;
    protein: number;
    carbs: number;
    fat: number;
    fiber?: number;
}): Promise<void> {
    await serverApi("/api/Foods", {
        method: "POST",
        body: {
            name: data.name,
            brand: data.brand || undefined,
            barcode: data.barcode || undefined,
            servingSize: data.servingSize,
            servingUnit: "g",
            caloriesPer100g: data.calories,
            proteinPer100g: data.protein,
            carbsPer100g: data.carbs,
            fatPer100g: data.fat,
            fiberPer100g: data.fiber || null,
            isVerified: false
        },
    });

    revalidatePath("/admin/ingredients");
    revalidatePath("/ingredients");
}

/**
 * Add a new ingredient (food) via the backend API (Server Action)
 */
export async function addIngredient(prevState: FormState, formData: FormData): Promise<FormState> {
    try {
        const name = formData.get("name") as string;
        const servingSize = parseFloat(formData.get("servingSize") as string) || 100;
        const servingUnit = formData.get("servingUnit") as string || "g";
        const calories = parseFloat(formData.get("calories") as string);
        const protein = parseFloat(formData.get("protein") as string);
        const carbs = parseFloat(formData.get("carbs") as string);
        const fat = parseFloat(formData.get("fat") as string);
        const fiber = parseFloat(formData.get("fiber") as string);
        const isVerified = formData.get("isVerified") === "true";

        await serverApi("/api/Foods", {
            method: "POST",
            body: {
                name,
                servingSize,
                servingUnit,
                caloriesPer100g: calories,
                proteinPer100g: protein,
                carbsPer100g: carbs,
                fatPer100g: fat,
                fiberPer100g: isNaN(fiber) ? null : fiber,
                isVerified
            },
        });

        revalidatePath("/admin/ingredients");

        return createSuccessState("Ingredient added successfully!");
    } catch (error) {
        ingredientLogger.error("Failed to add ingredient", { error });
        return createErrorState("Failed to add ingredient");
    }
}

export async function updateIngredientData(id: string, data: {
    name: string;
    brand?: string;
    barcode?: string;
    servingSize: number;
    calories: number;
    protein: number;
    carbs: number;
    fat: number;
    fiber?: number;
    isVerified: boolean;
}): Promise<void> {
    await serverApi(`/api/Foods/${id}`, {
        method: "PUT",
        body: {
            id,
            name: data.name,
            brand: data.brand || undefined,
            barcode: data.barcode || undefined,
            servingSize: data.servingSize,
            servingUnit: "g",
            caloriesPer100g: data.calories,
            proteinPer100g: data.protein,
            carbsPer100g: data.carbs,
            fatPer100g: data.fat,
            fiberPer100g: data.fiber || null,
            isVerified: data.isVerified
        },
    });

    revalidatePath("/ingredients");
    revalidatePath(`/ingredients/${id}`);
}

/**
 * Update an existing ingredient via the backend API (Server Action)
 */
export async function updateIngredient(prevState: FormState, formData: FormData): Promise<FormState> {
    try {
        const id = formData.get("id") as string;
        const name = formData.get("name") as string;
        const servingSize = parseFloat(formData.get("servingSize") as string) || 100;
        const servingUnit = formData.get("servingUnit") as string || "g";
        const calories = parseFloat(formData.get("calories") as string);
        const protein = parseFloat(formData.get("protein") as string);
        const carbs = parseFloat(formData.get("carbs") as string);
        const fat = parseFloat(formData.get("fat") as string);
        const fiber = parseFloat(formData.get("fiber") as string);
        const isVerified = formData.get("isVerified") === "true";

        await serverApi(`/api/Foods/${id}`, {
            method: "PUT",
            body: {
                id,
                name,
                servingSize,
                servingUnit,
                caloriesPer100g: calories,
                proteinPer100g: protein,
                carbsPer100g: carbs,
                fatPer100g: fat,
                fiberPer100g: isNaN(fiber) ? null : fiber,
                isVerified
            },
        });

        revalidatePath("/admin/ingredients");

        return createSuccessState("Ingredient updated successfully!");
    } catch (error) {
        ingredientLogger.error("Failed to update ingredient", { error });
        return createErrorState("Failed to update ingredient");
    }
}

/**
 * Delete an ingredient entry
 */
export async function deleteIngredient(id: string): Promise<{ success: boolean; message?: string }> {
    try {
        await serverApi(`/api/Foods/${id}`, {
            method: "DELETE",
        }).then(() => {

            revalidatePath("/admin/ingredients");
        });

        return { success: true };
    } catch (error) {
        ingredientLogger.error("Failed to delete ingredient", { error, ingredientId: id });
        return { success: false, message: "Failed to delete ingredient" };
    }
}
