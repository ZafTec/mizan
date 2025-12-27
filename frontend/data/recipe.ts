"use server";

import { callBackendApi } from "@/lib/backend-api-client";
import { createErrorState, createSuccessState, FormState } from "@/helper/FormErrorHandler";
import type { components } from "@/types/api.generated";

// Base generated types
export type RecipeIngredient = components["schemas"]["RecipeIngredientDto"];
export type RecipeInstruction = components["schemas"]["RecipeInstructionDto"];

// Extended nutrition type with fiber support
export interface RecipeNutrition {
    caloriesPerServing?: number | null;
    proteinGrams?: number | null;
    carbsGrams?: number | null;
    fatGrams?: number | null;
    fiberGrams?: number | null;
}

// Extended recipe types with fiber in nutrition
export interface Recipe extends Omit<components["schemas"]["RecipeDetailDto"], "nutrition"> {
    nutrition?: RecipeNutrition;
}

export interface RecipeDto extends Omit<components["schemas"]["RecipeDto"], "nutrition"> {
    nutrition?: RecipeNutrition;
}

export interface PopularRecipe {
    _id: string;
    name: string;
    totalMacros: {
        calories: number;
        protein: number;
        carbs: number;
        fat: number;
    };
}

/**
 * Get popular recipes for the homepage from the backend API.
 */
export async function getPopularRecipes(): Promise<PopularRecipe[]> {
    try {
        const result = await callBackendApi<{ recipes: RecipeDto[] }>("/api/Recipes?IncludePublic=true&PageSize=6");

        return (result.recipes || []).map((r) => ({
            _id: r.id || "",
            name: r.title || "",
            totalMacros: {
                calories: r.nutrition?.caloriesPerServing || 0,
                protein: r.nutrition?.proteinGrams || 0,
                carbs: r.nutrition?.carbsGrams || 0,
                fat: r.nutrition?.fatGrams || 0,
            },
        }));
    } catch (error) {
        // If not authenticated, return empty array (public recipes require auth in BFF)
        console.error("Failed to get popular recipes:", error);
        return [];
    }
}

/**
 * Get all recipes with optional filters
 */
export async function getAllRecipes(searchTerm?: string, page: number = 1, limit: number = 20, favoritesOnly: boolean = false): Promise<{ recipes: RecipeDto[], totalCount: number, totalPages: number }> {
    try {
        const params = new URLSearchParams();
        if (searchTerm) params.append("SearchTerm", searchTerm);
        if (favoritesOnly) params.append("FavoritesOnly", "true");
        params.append("IncludePublic", "true");
        params.append("Page", page.toString());
        params.append("PageSize", limit.toString());

        const result = await callBackendApi<{ recipes: RecipeDto[], totalCount: number, page: number, pageSize: number }>(`/api/Recipes?${params.toString()}`);

        const totalPages = Math.ceil((result.totalCount || 0) / limit);

        return {
            recipes: result.recipes || [],
            totalCount: result.totalCount || 0,
            totalPages
        };
    } catch (error) {
        console.error("Failed to get all recipes:", error);
        return { recipes: [], totalCount: 0, totalPages: 0 };
    }
}

/**
 * Get favorite recipes count for the current user
 */
export async function getFavoriteRecipesCount(): Promise<number> {
    const result = await getAllRecipes(undefined, 1, 1, true);
    return result.totalCount;
}

/**
 * Get a recipe by ID from the backend API.
 */
export async function getRecipeById(recipeId: string): Promise<Recipe | null> {
    try {
        const result = await callBackendApi<Recipe>(`/api/Recipes/${recipeId}`);
        return result;
    } catch (error) {
        console.error("Failed to get recipe:", error);
        return null;
    }
}
