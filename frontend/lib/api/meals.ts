import { clientApi } from "@/lib/api.client";
import type { components } from "@/types/api.generated";

/**
 * Browser calls for the meal form. Types come from the generated OpenAPI
 * document. The preview writes nothing: the server says what each line and
 * the meal come to, so the form shows numbers and computes none.
 */
type Schemas = components["schemas"];

export type MealPreviewLine = Schemas["MealPreviewLine"];
export type MealPreviewResult = Schemas["MealPreviewResult"];

export const previewMeal = (lines: MealPreviewLine[]) =>
  clientApi<MealPreviewResult>("/api/Meals/preview", {
    method: "POST",
    body: { lines } satisfies Schemas["PreviewMealQuery"],
  });

export type RecipeFitResult = Schemas["RecipeFitResult"];
export type RecipeFitItem = Schemas["RecipeFitItem"];

/**
 * The person's saved recipes, best first, by how well one serving fits what is
 * left of the day's targets. The ranking and every figure are the API's.
 */
export const recipesThatFit = (date: string, count = 3) =>
  clientApi<RecipeFitResult>(
    `/api/Recipes/fit?${new URLSearchParams({ date, count: String(count) })}`,
  );
