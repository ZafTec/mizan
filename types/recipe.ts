import Ingredient from "./ingredient";
import Macros from "./macro";
import { ID } from "./id";

export default interface Recipe {
	id: ID;
	// userId: ID;
	name: string;
	images: string[];
	description?: string;
	ingredients: {
		ingredient: Ingredient;
		amount: number;
		unit: string;
	}[];
	totalMacros: Macros; // calculated from ingredients
	servings: number;
	instructions: string[];
	tags: string[];

	// timestamps
	createdAt: Date;
	updatedAt: Date;
}

export interface RecipeInput {
	name: string;
	images: string[];
	description?: string;
	ingredients: {
		ingredient: Ingredient;
		amount: number;
		unit: string;
	}[];
	servings: number;
	instructions: string[];
	tags: string[];
}
