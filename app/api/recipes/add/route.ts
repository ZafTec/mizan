
import { NextRequest, NextResponse } from "next/server";
import { RecipeInput } from "@/types/recipe";
import { addRecipe } from "@/data/recipe";

export async function POST(request: NextRequest) {
	try {
		// Parse the JSON body
		const body = await request.json();
		
		const { name, description, instructions, ingredients, servings, tags, images } = body;

		
		const recipeData: RecipeInput = {
			name,
			description,
			ingredients,
			instructions,
			servings,
			tags,
			images
		};

		// Save the recipe to the database
		await addRecipe(recipeData);
		// console.log(recipeData);
		
		return NextResponse.json({ message: "Add Ingredients." });
	} catch (error) {
		console.error('Error parsing request body:', error);
		return NextResponse.json(
			{ error: 'Invalid request body' },
			{ status: 400 }
		);
	}
}
