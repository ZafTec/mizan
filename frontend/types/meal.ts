
import { ID } from "./id";
import Macros from "./macro";
import User from "./user";

// an enum of category, snack, meal, drink
export enum MealType {
	Meal = "Meal",
	Snack = "Snack",
	Drink = "Drink",
}


export default interface Meal {
	_id: ID;

	name: string;
	mealType: MealType;

	totalMacros: Macros;

    user: User;

	// timestamps
	createdAt: Date;
	updatedAt: Date;
}


export interface PerDayMealsAggregate {
	date: Date;
	totalMacros: Macros;
	meals: Meal[];
}

export interface MealInput {
	name: string;
	mealType: MealType;
	totalMacros: Macros;
}
