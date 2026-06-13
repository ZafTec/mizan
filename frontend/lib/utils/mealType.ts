// Canonical meal-type vocabulary shared by the log forms. Stored uppercase to
// match the backend (Mizan.Domain.Constants.MealTypes). "MEAL" is a legacy value
// kept only for display of historical rows; it is not offered as a new choice.

export const MEAL_TYPES = ["BREAKFAST", "LUNCH", "DINNER", "SNACK", "DRINK"] as const;
export type MealTypeValue = (typeof MEAL_TYPES)[number];

const LABELS: Record<string, string> = {
	BREAKFAST: "Breakfast",
	LUNCH: "Lunch",
	DINNER: "Dinner",
	SNACK: "Snack",
	DRINK: "Drink",
	MEAL: "Meal",
};

/** Picks a sensible default meal type from the local hour (0-23). */
export function defaultMealTypeForHour(hour: number): MealTypeValue {
	if (hour < 11) return "BREAKFAST";
	if (hour < 15) return "LUNCH";
	if (hour < 17) return "SNACK";
	if (hour < 21) return "DINNER";
	return "SNACK";
}

/** Human label for any stored value, regardless of casing. */
export function formatMealType(value: string | null | undefined): string {
	if (!value) return "";
	const upper = value.toUpperCase();
	return LABELS[upper] ?? value.charAt(0).toUpperCase() + value.slice(1).toLowerCase();
}
