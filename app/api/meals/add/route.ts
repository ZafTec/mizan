
import { NextRequest, NextResponse } from "next/server";
import { MealInput } from "@/types/meal";
import { addMeal } from "@/data/meal";

export async function POST(request: NextRequest) {
	try {
		// Parse the JSON body
		const body = await request.json();
		
		const { name, mealType, macros } = body;

		const mealData: MealInput = {
			name,
            mealType,
			totalMacros: macros
		};

		// Save the meal to the database
		await addMeal(mealData);
		
		return NextResponse.json({ message: "Add Meal." });
	} catch (error) {
		console.error('Error parsing request body:', error);
		return NextResponse.json(
			{ error: 'Invalid request body' },
			{ status: 400 }
		);
	}
}
