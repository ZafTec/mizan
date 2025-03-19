"use server";

import ingredient from "@/model/ingredient";
import Recipe from "@/model/recipe";
import MongoDBClient from "@/mongo/client";
import { ID } from "@/types/id";
import Macros from "@/types/macro";
import RecipeType, { RecipeInput } from "@/types/recipe";
import User from "@/types/user";


export async function getAllRecipes(
	searchRecipe: string = "",
	sortBy?: string
): Promise<RecipeType[]> {
	await MongoDBClient();
  
	const query = searchRecipe
		? { name: { $regex: new RegExp(searchRecipe, "i") } }
		: {};

	let sortOptions = {};
	if (sortBy === "Name") {
		sortOptions = { name: 1 };
	} else if (sortBy === "Date") {
		sortOptions = { createdAt: -1 };
	} else if (sortBy === "Calories") {
		sortOptions = { "totalMacros.calories": 1 };
	}

	const recipes = await Recipe.find(query).sort(sortOptions).populate("creator");
	return recipes;
}

export async function getUserRecipes(userId: ID): Promise<RecipeType[]> {
	await MongoDBClient();
  
	const recipes = await Recipe.find({ creator: userId });
	return recipes;
}

export async function getRecipeById(id: string): Promise<RecipeType | null> {
	await MongoDBClient();
    
    await ingredient.find();  // FIXME: This is a dummy line to fix the unregeistered model error

	const recipe = await Recipe.findById(id)
        .populate("creator")
        .populate({
            path: "ingredients.ingredient",
			model: "Ingredient"
        });
        
	return recipe;
}

export async function addRecipe(recipe: RecipeInput, user: User): Promise<RecipeType> {
	await MongoDBClient();

	// Process ingredients to set the correct isRecipe value
	const processedIngredients = recipe.ingredients.map(ing => {
		if (ing.isRecipe) {
			return {
				...ing,
				isRecipe: 'Recipe',
			};
		} else {
			return {
				...ing,
				isRecipe: 'Ingredient',
			};
		}
	});

	// Calculate totalMacros
	const totalMacros: Macros = {
		calories: 0,
		protein: 0,
		fat: 0,
		carbs: 0,
		fiber: 0,
	};

	// Loop through ingredients to calculate macros
	for (const ing of processedIngredients) {
		let ingredientMacros;
		let servingRatio;
		
		if (ing.isRecipe === 'Recipe') {
			// Handle recipe as ingredient
			const recipeIngredient = ing.ingredient as RecipeType;
			ingredientMacros = recipeIngredient.totalMacros;
			servingRatio = ing.amount / recipeIngredient.servings;
		} else {
			// Handle regular ingredient
			const regularIngredient = ing.ingredient as any; // Using any for now
			ingredientMacros = regularIngredient.macros;
			servingRatio = ing.amount / regularIngredient.servingSize;
		}

		totalMacros.calories += ingredientMacros.calories * servingRatio;
		totalMacros.protein += ingredientMacros.protein * servingRatio;
		totalMacros.fat += ingredientMacros.fat * servingRatio;
		totalMacros.carbs += ingredientMacros.carbs * servingRatio;
		totalMacros.fiber += ingredientMacros.fiber * servingRatio;
	}

	const newRecipe = await Recipe.create({
		name: recipe.name,
		description: recipe.description,
		ingredients: processedIngredients,
		totalMacros,
		servings: recipe.servings,
		instructions: recipe.instructions,
		tags: recipe.tags,
		images: recipe.images,
		creator: user._id,
	});
	return newRecipe;
}

export async function getPopularRecipes(): Promise<RecipeType[]> {
    await MongoDBClient();

    const recipes = await Recipe.find().sort({ createdAt: -1 }).limit(6);
    return recipes;
}
