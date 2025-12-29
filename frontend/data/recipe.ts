"use server";

import { callBackendApi } from "@/lib/backend-api-client";
import type { components } from "@/types/api.generated";
import { logger } from "@/lib/logger";

const recipeLogger = logger.createModuleLogger("recipe-data");


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
        const result = await callBackendApi<{ recipes: RecipeDto[] }>(
            "/api/Recipes?IncludePublic=true&PageSize=6",
            { requireAuth: false }
        );

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
        recipeLogger.error("Failed to get popular recipes", { error });
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

        // FavoritesOnly requires auth, but public recipes don't
        const result = await callBackendApi<{ recipes: RecipeDto[], totalCount: number, page: number, pageSize: number }>(
            `/api/Recipes?${params.toString()}`,
            { requireAuth: favoritesOnly }
        );

        const totalPages = Math.ceil((result.totalCount || 0) / limit);

        return {
            recipes: result.recipes || [],
            totalCount: result.totalCount || 0,
            totalPages
        };
    } catch (error) {
        recipeLogger.error("Failed to get all recipes", { error, searchTerm, page, limit, favoritesOnly });
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
 * Public recipes can be viewed without authentication.
 */
export async function getRecipeById(recipeId: string): Promise<Recipe | null> {
    try {
        const result = await callBackendApi<Recipe>(
            `/api/Recipes/${recipeId}`,
            { requireAuth: false }
        );
        return result;
    } catch (error) {
        recipeLogger.error("Failed to get recipe by ID", { error, recipeId });
        return null;
    }
}
