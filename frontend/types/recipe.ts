import Ingredient from "./ingredient";

// Recipe type matching the .NET API response
export default interface Recipe {
	id: string;
	title: string;
	description?: string;
	imageUrl?: string;
	servings: number;
	prepTimeMinutes?: number;
	cookTimeMinutes?: number;
	calories?: number;
	protein?: number;
	carbs?: number;
	fat?: number;
	fiber?: number;
	ingredients?: RecipeIngredient[];
	instructions?: string[];
	tags?: string[];
	creatorId?: string;
	isPublic: boolean;
	createdAt: string;
	updatedAt: string;
}

export interface RecipeIngredient {
	ingredientText: string;
	foodName?: string;
	amount?: number;
	unit?: string;
}

export interface RecipeInput {
	title: string;
	description?: string;
	imageUrl?: string;
	ingredients: {
		ingredient: Ingredient;
		amount: number;
		unit: string;
	}[];
	servings: number;
	instructions: string[];
	tags: string[];
}
