"use server";

import { FormState, fromErrorToFormState } from "@/helper/FormErrorHandler";
import { toFormState } from "@/helper/toFormState";
import Meal from "@/model/meal";
import MongoDBClient from "@/mongo/client";
import Macros from "@/types/macro";
import MealType, { MealInput, PerDayMealsAggregate } from "@/types/meal";
import { TimeSpan } from "@/types/timespan";
import { ObjectId } from "mongoose";
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

export async function getMeal(timeSpan: TimeSpan = TimeSpan.Week): Promise<PerDayMealsAggregate[]> {
	await MongoDBClient();

	const now = new Date();
	let startDate: Date;

	switch (timeSpan) {			
		case TimeSpan.Week:
			startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 6); // Last 7 days
			break;
		case TimeSpan.Month:
			startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 29); // Last 30 days
			break;
		case TimeSpan.Year:
			startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 364); // Last 365 days
			break;
		default:
			startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 6);
			break;
	}
  
	const meals: MealType[] = await Meal.find({
		createdAt: {
			$gte: startDate,
			$lte: now,
		},
	})
  
	// Group meals by date
	const mealsByDate: { [dateStr: string]: MealType[] } = {};
  
	meals.forEach((meal) => {
		const dateStr = meal.createdAt.toISOString().substring(0, 10); // 'YYYY-MM-DD'
		if (!mealsByDate[dateStr]) {
			mealsByDate[dateStr] = [];
		}
		mealsByDate[dateStr].push(meal);
	});
  
	// Aggregate data per day
	const perDayMealsAggregate: PerDayMealsAggregate[] = Object.keys(mealsByDate).map((dateStr) => {
		const dateMeals = mealsByDate[dateStr];
		const totalMacros: Macros = {
			calories: 0,
			protein: 0,
			carbs: 0,
			fat: 0,
			fiber: 0,
		};
	
		dateMeals.forEach((meal) => {
			totalMacros.calories += meal.totalMacros.calories;
			totalMacros.protein += meal.totalMacros.protein;
			totalMacros.carbs += meal.totalMacros.carbs;
			totalMacros.fat += meal.totalMacros.fat;
			totalMacros.fiber += meal.totalMacros.fiber;
		})
	
		return {
			date: new Date(dateStr),
			totalMacros,
			meals: dateMeals,
		}
	})
  
	// Sort by date ascending
	perDayMealsAggregate.sort((a, b) => a.date.getTime() - b.date.getTime());
  
	return perDayMealsAggregate;
}

export async function deleteMeal(id: string | ObjectId): Promise<void> {
	await MongoDBClient();

	await Meal.findByIdAndDelete(id);
}
