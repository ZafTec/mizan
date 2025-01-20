"use server";

import Meal from "@/model/meal";
import MongoDBClient from "@/mongo/client";
import MealType, { MealInput } from "@/types/meal";


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

export async function addMeal(meal: MealInput): Promise<MealType> {
	await MongoDBClient();

	const newMeal = await Meal.create({
		name: meal.name,
        mealType: meal.mealType,
		totalMacros: meal.totalMacros,
	});
	return newMeal;
}
