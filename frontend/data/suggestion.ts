"use server";

import { callBackendApi } from "@/lib/backend-api-client";

export interface SuggestedRecipe {
    id: string;
    title: string;
    description?: string;
    calories: number;
    protein: number;
    carbs: number;
    fat: number;
    imageUrl?: string;
    prepTime?: number;
    cookTime?: number;
    reason: string;
}

/**
 * Get recipe suggestions for today based on remaining macros
 * Uses AI chat endpoint for personalized recommendations
 */
export async function getTodaySuggestions(): Promise<SuggestedRecipe[]> {
    try {
        // Use the AI chat endpoint to get suggestions
        const response = await callBackendApi<{ response: string }>("/api/Nutrition/ai/chat", {
            method: "POST",
            body: JSON.stringify({
                message: "Suggest 3-5 recipes that would fit my remaining macros for today. Return them as a JSON array with fields: id, title, description, calories, protein, carbs, fat.",
            }),
        });

        // Try to parse suggestions from AI response
        try {
            const jsonMatch = response.response?.match(/\[[\s\S]*\]/);
            if (jsonMatch) {
                const suggestions = JSON.parse(jsonMatch[0]);
                return suggestions.map((s: any) => ({
                    ...s,
                    reason: "AI recommended based on your remaining macros",
                }));
            }
        } catch {
            // AI didn't return parseable JSON, fallback to empty
        }

        return [];
    } catch (error) {
        console.error("Failed to get suggestions:", error);
        return [];
    }
}

/**
 * Regenerate suggestions fresh
 */
export async function regenerateSuggestions(): Promise<SuggestedRecipe[]> {
    return getTodaySuggestions();
}
