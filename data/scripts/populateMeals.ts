// scripts/populateMeals.ts

import MongoDBClient from '@/mongo/client';
import Macros from '@/types/macro';
import Meal, { MealType } from '@/types/meal';
import mongoose from 'mongoose';
import MealModel from '@/model/meal';

async function populateMeals() {
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
	const mealTypes = [MealType.Meal, MealType.Snack, MealType.Drink];

	// Array to hold meal data
	const mealsData: Partial<Meal>[] = [];

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
		await MealModel.insertMany(mealsData);
		console.log('Dummy meals data inserted successfully.');
	} catch (error) {
		console.error('Error inserting dummy meals data:', error);
	} finally {
		mongoose.connection.close();
		process.exit();
	}
}

populateMeals();

// npx ts-node scripts/populateMeals.ts
