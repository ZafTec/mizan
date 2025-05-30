import MongoDBClient from "@/mongo/client";
import { RecipeSchema } from "@/types/openai";
import Recipe from "@/types/recipe";
import { z } from "zod";
import Ingredient from "@/model/ingredient";
import RecipeModel from "@/model/recipe";
import { ID } from "@/types/id";
import Macros from "@/types/macro";
import mongoose from "mongoose";

type RecipeFromIngredients = z.infer<typeof RecipeSchema>;

export async function recipesFromIngredients(data: RecipeFromIngredients, userId: ID): Promise<Recipe> {
    await MongoDBClient();
    
    try {     
        if (data.fromSystem) {
            // fetch existing recipe from the system
            const existingRecipe = await RecipeModel.findById(data.fromSystem.id)
                .populate("creator");
            if (existingRecipe) {
                // If the recipe already exists, return it
                return existingRecipe as unknown as Recipe;
            }
        }
        // Get all ingredient IDs from the data
        const ingredientIds = data.ingredients.map(ing => ing.id);
        
        // Find all ingredients by their IDs
        const ingredients = await Ingredient.find({
            _id: { $in: ingredientIds.map(id => {
                try {
                    return new mongoose.Types.ObjectId(id);
                } catch (error) {
                    // If ID is invalid, return a dummy ObjectId that won't match
                    return new mongoose.Types.ObjectId();
                }
            })}
        });
        
        // Create a map of ingredient ID to ingredient for easier lookup
        const ingredientMap = new Map();
        ingredients.forEach(ing => {
            ingredientMap.set(ing._id.toString(), ing);
        });
        
        // Initialize total macros
        const totalMacros: Macros = {
            calories: 0,
            protein: 0,
            carbs: 0,
            fat: 0,
            fiber: 0
        };
        
        // Process ingredients and calculate macros
        const processedIngredients = data.ingredients.map(ing => {
            const ingredient = ingredientMap.get(ing.id);
            
            // If ingredient is found, calculate macros contribution
            if (ingredient) {
                const servingRatio = ing.amount / ingredient.servingSize;
                
                // Add to total macros
                totalMacros.calories += ingredient.macros.calories * servingRatio;
                totalMacros.protein += ingredient.macros.protein * servingRatio;
                totalMacros.carbs += ingredient.macros.carbs * servingRatio;
                totalMacros.fat += ingredient.macros.fat * servingRatio;
                totalMacros.fiber += ingredient.macros.fiber * servingRatio;
                
                // Return processed ingredient
                return {
                    ingredient: ingredient._id,
                    amount: ing.amount,
                    unit: ingredient.servingUnit,
                    isRecipe: 'Ingredient'
                };
            }
            
            // If ingredient not found, return null (will be filtered out later)
            return null;
        }).filter(ing => ing !== null); // Remove null entries
        
        // Create new recipe
        const newRecipe = await RecipeModel.create({
            name: data.name,
            description: data.description,
            ingredients: processedIngredients,
            totalMacros: totalMacros,
            servings: 1, // Default to 1 serving
            instructions: data.instructions,
            tags: ["suggestion", "ai-generated"],
            images: [], // No images initially
            creator: userId
        });
        
        // Populate the creator field for the returned recipe
        const populatedRecipe = await RecipeModel.findById(newRecipe._id)
            .populate("creator")
            .populate({
                path: "ingredients.ingredient",
                model: "Ingredient"
            });
            
        return populatedRecipe as unknown as Recipe;
    } catch (error) {
        console.error("Error in recipesFromIngredients:", error);
        throw new Error(`Failed to create recipe from ingredients: ${error}`);
    }
}