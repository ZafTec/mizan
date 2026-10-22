import { resolvePublicApiOrigin } from "@/lib/api-base";
import { clientApi } from "@/lib/api.client";
import type { GamificationFeedback } from "@/types/gamification";
import type { components } from "@/types/api.generated";

/**
 * One food the assistant recognized: its guessed portion with totals, and the
 * same nutrition per 100 g so the server can scale to the grams the person
 * confirms.
 */
export type RecognizedFood = components["schemas"]["RecognizedFood"];
export type NutritionPer100g = components["schemas"]["NutritionPer100g"];
/** A proposal. The confidence is shown, not hidden: the numbers below it are estimates. */
export type FoodAnalysis = components["schemas"]["FoodAnalysisResult"];

/**
 * Multipart, so this goes through fetch rather than the JSON client. The
 * response is a proposal - nothing is written until the user confirms it.
 */
export async function analyzeFoodPhoto(file: File): Promise<FoodAnalysis> {
  const body = new FormData();
  body.append("image", file);

  const response = await fetch(
    `${resolvePublicApiOrigin()}/api/Nutrition/ai/analyze-image`,
    { method: "POST", credentials: "include", body },
  );

  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    throw new Error(
      payload?.error ??
        payload?.detail ??
        "The assistant could not read that photo.",
    );
  }

  return (await response.json()) as FoodAnalysis;
}

/** A sentence becomes a proposal the way a photo does. Nothing is logged until the person confirms it. */
export const analyzeFoodText = (description: string) =>
  clientApi<FoodAnalysis>("/api/Nutrition/ai/analyze-text", {
    method: "POST",
    body: { description },
  });

export type LogMealBody = components["schemas"]["CreateFoodDiaryEntryCommand"];

/**
 * A write the person may retry, so it carries an Idempotency-Key: the same key
 * sent twice logs one meal and answers with the first result.
 */
export const logMeal = (body: LogMealBody, idempotencyKey: string) =>
  clientApi<{ id: string } & GamificationFeedback>("/api/Meals", {
    method: "POST",
    body,
    headers: { "Idempotency-Key": idempotencyKey },
  });
