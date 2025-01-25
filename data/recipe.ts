"use server";

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

	const recipe = await Recipe.findById(id)
        .populate("creator")
        .populate("ingredients.ingredient");
	return recipe;
}

export async function addRecipe(recipe: RecipeInput, user: User): Promise<RecipeType> {
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
		creator: user._id,
	});
	return newRecipe;
}

export async function getPopularRecipes(): Promise<RecipeType[]> {
    await MongoDBClient();

    const recipes = await Recipe.find().sort({ createdAt: -1 }).limit(6);
    return recipes;
}
