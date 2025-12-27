// Ingredient type matching the .NET API response
export default interface Ingredient {
    id: string;
    name: string;
    brand?: string | null;
    servingSizeGrams: number;
    caloriesPer100g: number;
    proteinPer100g: number;
    carbsPer100g: number;
    fatPer100g: number;
    fiberPer100g: number;
    isVerified: boolean;
    createdAt: string;
    updatedAt: string;
}

// Legacy compatibility - provides macros in the old format
export interface IngredientWithMacros extends Ingredient {
    macros: {
        calories: number;
        protein: number;
        carbs: number;
        fat: number;
        fiber: number;
    };
    servingSize: number;
}
