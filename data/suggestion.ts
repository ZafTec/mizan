"use server";

import Suggestion from "@/model/suggestion";
import MongoDBClient from "@/mongo/client";
import { ID } from "@/types/id";
import Recipe from "@/types/recipe";
import { getRecipesSuggestion as getRecipesSuggestionHelper } from "@/data/meal";
import User from "@/types/user";

// Get today's suggestions for a user
export async function getTodaySuggestions(userId: ID): Promise<Recipe[]> {
  await MongoDBClient();

  // Get today's date boundaries (start of day and end of day)
  const todayStart = new Date();
  todayStart.setHours(0, 0, 0, 0);
  
  const todayEnd = new Date();
  todayEnd.setHours(23, 59, 59, 999);

  // Find suggestions for today
  const suggestions = await Suggestion.findOne({
    user: userId,
    createdAt: {
      $gte: todayStart,
      $lte: todayEnd,
    }
  })
  .sort({ createdAt: -1 }) // Sort by most recent
  .populate({
    path: "recipes",
    populate: [
      { path: "creator" },
      { 
        path: "ingredients.ingredient",
        model: "Ingredient"
      }
    ]
  });

  // If suggestions exist for today, return them
  if (suggestions && suggestions.recipes.length > 0) {
    return suggestions.recipes;
  }

  // If no suggestions for today, generate new ones
  const newRecipes = await getRecipesSuggestionHelper(userId);
  
  // Create a new suggestion document
  await Suggestion.create({
    user: userId,
    recipes: newRecipes.map(recipe => recipe._id),
  });

  return newRecipes;
}

// Regenerate suggestions for a user (admin only)
export async function regenerateSuggestions(user: User): Promise<Recipe[]> {
  // Check if user is admin
  if (!user.isAdmin) {
    throw new Error("Only admins can regenerate suggestions");
  }

  await MongoDBClient();

  // Delete today's suggestions if they exist
  const todayStart = new Date();
  todayStart.setHours(0, 0, 0, 0);
  
  const todayEnd = new Date();
  todayEnd.setHours(23, 59, 59, 999);

  await Suggestion.deleteOne({
    user: user._id,
    createdAt: {
      $gte: todayStart,
      $lte: todayEnd,
    }
  });

  // Generate new suggestions
  const newRecipes = await getRecipesSuggestionHelper(user._id);
  
  // Create a new suggestion document
  const newSuggestion = await Suggestion.create({
    user: user._id,
    recipes: newRecipes.map(recipe => recipe._id),
  });

  return newRecipes;
}
