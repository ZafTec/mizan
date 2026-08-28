import { resolvePublicApiOrigin } from "@/lib/api-base";
import { clientApi } from "@/lib/api.client";

export interface RecognizedFood {
	name: string;
	portionGrams: number;
	calories: number;
	protein: number;
	carbs: number;
	fat: number;
}

export interface FoodAnalysis {
	foods: RecognizedFood[];
	totalCalories: number;
	/** 0 to 1. Shown, not hidden: the numbers below it are estimates. */
	confidence: number;
	note?: string | null;
}

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
			payload?.error ?? payload?.detail ?? "The assistant could not read that photo.",
		);
	}

	return (await response.json()) as FoodAnalysis;
}

export interface LogMealBody {
	name: string;
	entryDate: string;
	mealType: string;
	servings: number;
	calories: number;
	proteinGrams: number;
	carbsGrams: number;
	fatGrams: number;
}

export const logMeal = (body: LogMealBody) =>
	clientApi<{ id: string }>("/api/Meals", { method: "POST", body });
