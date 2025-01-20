
import { ObjectId } from "mongoose";
import Macros from "./macro";

// an enum of category, snack, meal, drink
export enum MealType {
	Meal = "Meal",
	Snack = "Snack",
	Drink = "Drink",
}


export default interface Meal {
	_id: string | ObjectId;

	name: string;
    mealType: MealType;

	totalMacros: Macros;

	// timestamps
	createdAt: Date;
	updatedAt: Date;
}

export interface MealInput {
	name: string;
    mealType: MealType;
	totalMacros: Macros;
}
