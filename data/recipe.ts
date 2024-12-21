"use server";

import Recipe from "@/model/recipe";
import MongoDBClient from "@/mongo/client";
import RecipeType from "@/types/recipe";


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

export async function addRecipe(recipe: RecipeType): Promise<RecipeType> {
    await MongoDBClient();

    const newRecipe = await Recipe.create(recipe);
    return newRecipe;
}
