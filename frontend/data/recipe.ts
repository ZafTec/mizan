"use server";

import { serverApi } from "@/lib/api.server";
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
        const result = await serverApi<{ items: RecipeDto[] }>(
            "/api/Recipes?IncludePublic=true&PageSize=6",
            { requireAuth: false }
        );

        return (result.items || []).map((r) => ({
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
export async function getAllRecipes(searchTerm?: string, page: number = 1, limit: number = 20, favoritesOnly: boolean = false, sortBy?: string, sortOrder?: string): Promise<{ recipes: RecipeDto[], totalCount: number, totalPages: number }> {
    try {
        const params = new URLSearchParams();
        if (searchTerm) params.append("SearchTerm", searchTerm);
        if (favoritesOnly) params.append("FavoritesOnly", "true");
        params.append("IncludePublic", "true");
        params.append("Page", page.toString());
        params.append("PageSize", limit.toString());
        if (sortBy) params.append("SortBy", sortBy);
        if (sortOrder) params.append("SortOrder", sortOrder);

        const result = await serverApi<{ items: RecipeDto[], totalCount: number, page: number, pageSize: number, totalPages: number }>(
            `/api/Recipes?${params.toString()}`,
            { requireAuth: favoritesOnly }
        );

        return {
            recipes: result.items || [],
            totalCount: result.totalCount || 0,
            totalPages: result.totalPages || 0
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
        const result = await serverApi<Recipe>(
            `/api/Recipes/${recipeId}`,
            { requireAuth: false }
        );
        return result;
    } catch (error) {
        recipeLogger.error("Failed to get recipe by ID", { error, recipeId });
        return null;
    }
}
