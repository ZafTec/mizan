"use server";

import Recipe from "@/model/recipe";
import MongoDBClient from "@/mongo/client";
import Macros from "@/types/macro";
import RecipeType, { RecipeInput } from "@/types/recipe";


export async function getAllRecipe(searchRecipe: string = "", sortBy?: string): Promise<RecipeType[]> {
	await MongoDBClient();

	const recipes = await Recipe.find({
		name: {
			$regex: new RegExp(searchRecipe, "i"),
		},
	});
	return recipes;
}

export async function getRecipeById(id: string): Promise<RecipeType | null> {
	await MongoDBClient();

	const recipe = await Recipe.findById(id);
	return recipe;
}

export async function addRecipe(recipe: RecipeInput): Promise<RecipeType> {
	await MongoDBClient();

	// TODO: Calculate totalMacros
	let totalMacros: Macros = {
		calories: recipe.ingredients.reduce((acc, curr) => acc + curr.ingredient.macros.calories * (curr.amount / curr.ingredient.servingSize), 0),
		protein: recipe.ingredients.reduce((acc, curr) => acc + curr.ingredient.macros.protein * (curr.amount / curr.ingredient.servingSize), 0),
		fat: recipe.ingredients.reduce((acc, curr) => acc + curr.ingredient.macros.fat * (curr.amount / curr.ingredient.servingSize), 0),
		carbs: recipe.ingredients.reduce((acc, curr) => acc + curr.ingredient.macros.carbs * (curr.amount / curr.ingredient.servingSize), 0),
		fiber: recipe.ingredients.reduce((acc, curr) => acc + curr.ingredient.macros.fiber * (curr.amount / curr.ingredient.servingSize), 0),
	};


	const newRecipe = await Recipe.create({
		name: recipe.name,
		description: recipe.description,
		ingredients: recipe.ingredients,
		totalMacros,
		servings: recipe.servings,
		instructions: recipe.instructions,
		tags: recipe.tags,
		images: recipe.images,
	});
	return newRecipe;
}
