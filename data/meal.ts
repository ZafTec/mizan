"use server";

import { FormState, fromErrorToFormState } from "@/helper/FormErrorHandler";
import { toFormState } from "@/helper/toFormState";
import Meal from "@/model/meal";
import MongoDBClient from "@/mongo/client";
import MealType, { MealInput } from "@/types/meal";
import { TimeSpan } from "@/types/timespan";
import { revalidatePath } from "next/cache";
import { z } from "zod";


export async function getAllMeal(): Promise<MealType[]> {
	await MongoDBClient();

	const meals = await Meal.find({});
	return meals;
}

export async function getTodayMeal(): Promise<MealType[]> {
	await MongoDBClient();

	const meals = await Meal.find({
		createdAt: {
			$gte: new Date(new Date().setHours(0, 0, 0)),
			$lt: new Date(new Date().setHours(23, 59, 59)),
		},
	});
	return meals;
}


// Validation schema using Zod
const createMealSchema = z.object({
	name: z.string().min(1).max(100),
	mealType: z.enum(["Meal", "Snack", "Drink"]),
	calories: z.number().min(0),
	protein: z.number().min(0),
	fat: z.number().min(0),
	carbs: z.number().min(0),
	fiber: z.number().min(0),
});

export async function addMeal(formState: FormState, formData: FormData): Promise<FormState> {
	try {
		// Extract and parse form data
		const mealData = {
			name: formData.get("name"),
			mealType: formData.get("mealType"),
			calories: Number(formData.get("calories")!),
			protein: Number(formData.get("protein")!),
			fat: Number(formData.get("fat")!),
			carbs: Number(formData.get("carbs")!),
			fiber: Number(formData.get("fiber")!),
		};
  
		// Validate data using Zod schema
		const validatedData = createMealSchema.parse(mealData);
	
		await MongoDBClient();
	
		// Create new meal entry
		const newMeal = await Meal.create({
			name: validatedData.name,
			mealType: validatedData.mealType,
			totalMacros: {
			calories: validatedData.calories,
			protein: validatedData.protein,
			fat: validatedData.fat,
			carbs: validatedData.carbs,
			fiber: validatedData.fiber,
			},
		});
  
		if (!newMeal) {
			throw new Error("Failed to add meal");
		}
	} catch (error) {
		// Handle validation and other errors
		return fromErrorToFormState(error);
	}
  
	// Revalidate the page to reflect new data
	revalidatePath("/meals");
  
	return toFormState("SUCCESS", "Meal added successfully");
}

export async function getMeal(timeSpan: TimeSpan = TimeSpan.Week): Promise<MealType[]> {
	await MongoDBClient();

	let meals: MealType[] = [];

	switch (timeSpan) {			
		case TimeSpan.Week:
			meals = await Meal.find({
				createdAt: {
					$gte: new Date(new Date().setDate(new Date().getDate() - 7)),
				},
			});
			break;
		case TimeSpan.Month:
			meals = await Meal.find({
				createdAt: {
					$gte: new Date(new Date().setMonth(new Date().getMonth() - 1)),
				},
			});
			break;
		case TimeSpan.Year:
			meals = await Meal.find({
				createdAt: {
					$gte: new Date(new Date().setFullYear(new Date().getFullYear() - 1)),
				},
			});
			break;
		default:
			meals = await Meal.find({
                createdAt: {
                    $gte: new Date(new Date().setHours(0, 0, 0)),
                },
            });
            break;
	}

	return meals;
}
