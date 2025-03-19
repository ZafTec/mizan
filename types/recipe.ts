import Ingredient from "./ingredient";
import Macros from "./macro";
import { ID } from "./id";
import User from "./user";

// Define a union type for recipe ingredients
export type RecipeIngredientItem = Ingredient | Recipe;

export default interface Recipe {
	_id: ID;
	name: string;
	images: string[];
	description?: string;
	ingredients: {
		ingredient: RecipeIngredientItem;
		amount: number;
		unit: string;
		isRecipe?: boolean; // To distinguish between ingredients and recipes
	}[];
	totalMacros: Macros; // calculated from ingredients
	servings: number;
	instructions: string[];
	tags: string[];

	creator: User;

	// timestamps
	createdAt: Date;
	updatedAt: Date;
}

export interface RecipeInput {
	name: string;
	images: string[];
	description?: string;
	ingredients: {
		ingredient: RecipeIngredientItem;
		amount: number;
		unit: string;
		isRecipe?: boolean;
	}[];
	servings: number;
	instructions: string[];
	tags: string[];
}
