import Ingredient from "./ingredient";
import Macros from "./macro";
import { ID } from "./id";
import User from "./user";

export default interface Recipe {
	_id: ID;
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
		ingredient: Ingredient;
		amount: number;
		unit: string;
	}[];
	servings: number;
	instructions: string[];
	tags: string[];
}
