"use server";

import { FormState, fromErrorToFormState } from "@/helper/FormErrorHandler";
import { toFormState } from "@/helper/toFormState";
import Meal from "@/model/meal";
import MongoDBClient from "@/mongo/client";
import { ID } from "@/types/id";
import Macros from "@/types/macro";
import MealType, { MealType as MealTypeEnum, PerDayMealsAggregate } from "@/types/meal";
import { TimeSpan } from "@/types/timespan";
import { revalidatePath } from "next/cache";
import { z } from "zod";
import { getFullUserById } from "./user";
import User from "@/types/user";
import { getUserServer } from "@/helper/session";
import { getAllIngredient } from "./ingredient";
import { getCurrentGoal } from "./goal";
import { openAIChatRecipeSuggestions } from "@/utils/openAIChat";
import Recipe from "@/types/recipe";
import { recipesFromIngredients } from "@/helper/recipesFromIngredients";
import { getAllRecipes } from "./recipe";


export async function getAllMeal(): Promise<MealType[]> {
	await MongoDBClient();

	const meals = await Meal.find({});
	return meals;
}

export async function getTodayMeal(userId: ID): Promise<MealType[]> {
	await MongoDBClient();

	const meals = await Meal.find({
		createdAt: {
			$gte: new Date(new Date().setHours(0, 0, 0)),
			$lt: new Date(new Date().setHours(23, 59, 59)),
		},
		user: userId,
	});
	return meals;
}

export async function getNutritionOverview(userId: ID): Promise<Macros> {
	await MongoDBClient();

	const meals = await getTodayMeal(userId);

	const totalMacros: Macros = {
		calories: 0,
		protein: 0,
		carbs: 0,
		fat: 0,
		fiber: 0,
	};

	meals.forEach((meal) => {
		totalMacros.calories += meal.totalMacros.calories;
		totalMacros.protein += meal.totalMacros.protein;
		totalMacros.carbs += meal.totalMacros.carbs;
		totalMacros.fat += meal.totalMacros.fat;
		totalMacros.fiber += meal.totalMacros.fiber;
	});

	return totalMacros;
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
    const user = await getUserServer();
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
			user: user as User,
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
	revalidatePath("/")
  
	return toFormState("SUCCESS", "Meal added successfully");
}

export async function getMeal(userId: ID, timeSpan: TimeSpan = TimeSpan.Week): Promise<PerDayMealsAggregate[]> {
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
		user: userId
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

export async function deleteMeal(id: ID, userId: ID): Promise<void> {
	await MongoDBClient();

	const meal = await Meal.findById(id).populate("user");
	if (!meal) {
		throw new Error("Meal not found");
	}
	if (meal.user._id.toString() !== userId.toString()) {
		throw new Error("Unauthorized");
	}
	
	await Meal.findByIdAndDelete(id);
}

export async function populateMeals() {
	// Connect to MongoDB
	await MongoDBClient();

	// Today's date
	const today = new Date();

	// Function to generate random integer between min and max
	function randomInt(min: number, max: number) {
		return Math.floor(Math.random() * (max - min + 1)) + min;
	}

	// Meal names and types for random selection
	const mealNames = [
		'Breakfast Burrito',
		'Protein Shake',
		'Salad',
		'Chicken Sandwich',
		'Grilled Salmon',
		'Pasta',
		'Fruit Smoothie',
		'Steak',
		'Oatmeal',
		'Yogurt Parfait',
	];
	const mealTypes = [MealTypeEnum.Meal, MealTypeEnum.Snack, MealTypeEnum.Drink];

	// Array to hold meal data
	const mealsData: Partial<MealType>[] = [];

	// Generate meals for the last 30 days
	for (let i = 0; i < 30; i++) {
		const date = new Date(today);
		date.setDate(today.getDate() - i); // Subtract i days

		// Generate 3 meals per day
		for (let j = 0; j < 3; j++) {
			const mealTime = new Date(date);
			mealTime.setHours(8 + j * 4); // Meals at 8 AM, 12 PM, and 4 PM

			const macros: Macros = {
				calories: randomInt(200, 800),
				protein: randomInt(5, 50),
				carbs: randomInt(10, 100),
				fat: randomInt(5, 30),
				fiber: randomInt(0, 15),
			};

			mealsData.push({
				name: mealNames[randomInt(0, mealNames.length - 1)],
				mealType: mealTypes[randomInt(0, mealTypes.length - 1)],
				totalMacros: macros,
				createdAt: mealTime,
				updatedAt: mealTime,
			});
		}
	}

	// Insert the meals into the database
	try {
		await Meal.insertMany(mealsData);
		console.info('Dummy meals data inserted successfully.');
	} catch (error) {
		console.error('Error inserting dummy meals data:', error);
	}
}

export async function getRecipesSuggestion(userId: ID): Promise<Recipe[]> {
	await MongoDBClient();

	// Fetch meals for the user
	const todaysMeal = await getTodayMeal(userId);
	const ingredients = await getAllIngredient();
	const currentGoal = await getCurrentGoal(userId);
	const allRecipes = await getAllRecipes();

	// Get openAI meal suggestions
	const suggestions = await openAIChatRecipeSuggestions({
		ingredients: ingredients,
		currentGoal: currentGoal,
		mealsConsumedToday: todaysMeal,
		recipesInSystem: allRecipes,
	});
	console.debug("Total macros from OpenAI suggestions:", suggestions.totalMacros);

	const recipes = await Promise.all(suggestions.recipes.map(async (recipe) => {
		return await recipesFromIngredients(recipe, userId);
	}));

	return recipes;
	// return recipes.filter((recipe) => recipe !== null) as Recipe[];
}
