"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";

const suggestionLogger = logger.createModuleLogger("suggestion-data");

export interface SuggestedRecipe {
	title: string;
	description: string;
	calories: number;
	protein: number;
	carbs: number;
	fat: number;
	reason: string;
}

interface MealSuggestionResult {
	suggestions: SuggestedRecipe[];
	note?: string | null;
}

/**
 * Meal ideas for the rest of today.
 *
 * This used to send a "return JSON" instruction to the chat endpoint and pull
 * an array out of the reply with a regex. That is the failure §10 rules out:
 * a half-scraped answer looks exactly like a real one. It is now its own
 * prompt key with a declared schema, so a malformed response is a failed call
 * and this returns nothing rather than something plausible.
 */
export async function getTodaySuggestions(): Promise<SuggestedRecipe[]> {
	try {
		const result = await serverApi<MealSuggestionResult>("/api/Ai/suggestions", {
			method: "POST",
		});
		return result.suggestions ?? [];
	} catch (error) {
		// A quota ceiling or an unconfigured assistant is an empty list, not a
		// broken page - the rest of the dashboard does not depend on this.
		suggestionLogger.warn("No suggestions available", { error });
		return [];
	}
}

export async function regenerateSuggestions(): Promise<SuggestedRecipe[]> {
	return getTodaySuggestions();
}
