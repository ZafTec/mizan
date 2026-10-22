"use server";

import { serverApi } from "@/lib/api.server";
import { ApiError } from "@/lib/api";
import { logger } from "@/lib/logger";
import type { components } from "@/types/api.generated";

const mealPlanLogger = logger.createModuleLogger("meal-plan-data");

function apiErrorMessage(error: unknown, fallback: string): string {
  if (
    error instanceof ApiError &&
    error.body &&
    typeof error.body === "object"
  ) {
    const message = (error.body as { error?: unknown }).error;
    if (typeof message === "string" && message.trim()) return message;
  }
  return fallback;
}

type Schemas = components["schemas"];

export type MealPlanRecipe = Schemas["MealPlanRecipeDetailDto"];
export type MealPlanNutritionSummary = Schemas["MealPlanNutritionSummaryDto"];
/** One plan with its recipes, as GET /api/MealPlans/{id} returns it. */
export type MealPlan = Schemas["MealPlanDetailDto"];
/** A plan as the list endpoint returns it: a recipe count, not the recipes. */
export type MealPlanSummary = Schemas["MealPlanDto"];

export interface MealPlanListResult {
  mealPlans: MealPlanSummary[];
  totalCount: number;
  totalPages: number;
}

export async function getMealPlans(
  page: number = 1,
  pageSize: number = 20,
  sortBy?: string,
  sortOrder?: string,
): Promise<MealPlanListResult> {
  try {
    const params = new URLSearchParams();
    params.append("Page", page.toString());
    params.append("PageSize", pageSize.toString());
    if (sortBy) params.append("SortBy", sortBy);
    if (sortOrder) params.append("SortOrder", sortOrder);

    const result = await serverApi<Schemas["MealPlanDtoPagedResult"]>(
      `/api/MealPlans?${params}`,
    );
    return {
      mealPlans: result.items || [],
      totalCount: result.totalCount || 0,
      totalPages: result.totalPages || 0,
    };
  } catch (error) {
    mealPlanLogger.error("Failed to get meal plans", { error });
    return { mealPlans: [], totalCount: 0, totalPages: 0 };
  }
}

export async function getMealPlanById(id: string): Promise<MealPlan | null> {
  try {
    return await serverApi<MealPlan>(`/api/MealPlans/${id}`);
  } catch (error) {
    mealPlanLogger.error("Failed to get meal plan", { error, id });
    return null;
  }
}

export async function createMealPlan(data: {
  name: string;
  startDate: string;
  endDate: string;
  recipes?: {
    recipeId: string;
    date: string;
    mealType: string;
    servings: number;
  }[];
}): Promise<{ id: string; success: boolean; message?: string } | null> {
  try {
    const result = await serverApi<Schemas["CreateMealPlanResult"]>(
      "/api/MealPlans",
      {
        method: "POST",
        body: data,
      },
    );
    return { id: result.id, success: true };
  } catch (error) {
    mealPlanLogger.error("Failed to create meal plan", { error });
    return {
      id: "",
      success: false,
      message: apiErrorMessage(error, "Failed to create meal plan"),
    };
  }
}

export async function addRecipeToMealPlan(
  mealPlanId: string,
  data: { recipeId: string; date: string; mealType: string; servings: number },
): Promise<boolean> {
  try {
    await serverApi<Schemas["AddRecipeToMealPlanResult"]>(
      `/api/MealPlans/${mealPlanId}/recipes`,
      {
        method: "POST",
        body: data,
      },
    );
    return true;
  } catch (error) {
    mealPlanLogger.error("Failed to add recipe to meal plan", {
      error,
      mealPlanId,
    });
    return false;
  }
}

export async function updateMealPlan(
  id: string,
  data: { name: string; startDate: string; endDate: string },
): Promise<{ success: boolean; message?: string }> {
  try {
    const result = await serverApi<Schemas["UpdateMealPlanResult"]>(
      `/api/MealPlans/${id}`,
      {
        method: "PUT",
        body: data,
      },
    );
    return { success: result.success };
  } catch (error) {
    mealPlanLogger.error("Failed to update meal plan", { error, id });
    return { success: false, message: "Failed to update meal plan" };
  }
}

export async function removeRecipeFromMealPlan(
  mealPlanId: string,
  mealPlanRecipeId: string,
): Promise<{ success: boolean; message?: string }> {
  try {
    const result = await serverApi<Schemas["RemoveRecipeFromMealPlanResult"]>(
      `/api/MealPlans/${mealPlanId}/recipes/${mealPlanRecipeId}`,
      { method: "DELETE" },
    );
    return { success: result.success };
  } catch (error) {
    mealPlanLogger.error("Failed to remove recipe from meal plan", {
      error,
      mealPlanId,
      mealPlanRecipeId,
    });
    return {
      success: false,
      message: "Failed to remove recipe from meal plan",
    };
  }
}

export async function updateMealPlanRecipe(
  mealPlanId: string,
  mealPlanRecipeId: string,
  data: { date: string; mealType: string; servings: number },
): Promise<{ success: boolean; message?: string }> {
  try {
    const result = await serverApi<Schemas["UpdateMealPlanRecipeResult"]>(
      `/api/MealPlans/${mealPlanId}/recipes/${mealPlanRecipeId}`,
      { method: "PUT", body: data },
    );
    return { success: result.success };
  } catch (error) {
    mealPlanLogger.error("Failed to update meal plan recipe", {
      error,
      mealPlanId,
      mealPlanRecipeId,
    });
    return { success: false, message: "Failed to update meal plan recipe" };
  }
}

export async function deleteMealPlan(id: string): Promise<boolean> {
  try {
    await serverApi<Schemas["DeleteMealPlanResult"]>(`/api/MealPlans/${id}`, {
      method: "DELETE",
    });
    return true;
  } catch (error) {
    mealPlanLogger.error("Failed to delete meal plan", { error, id });
    return false;
  }
}
