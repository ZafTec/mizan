"use server";

import MealPlan from "@/model/mealPlan";
import MongoDBClient from "@/mongo/client";
import { ID } from "@/types/id";
import Macros from "@/types/macro";
import MealPlanType, { MealPlanInput, ShoppingListItem } from "@/types/mealPlan";
import { format, startOfWeek, endOfWeek, addDays } from "date-fns";
import RecipeType from "@/types/recipe";
import { getUserServer } from "@/helper/session";
import Recipe from "@/model/recipe";
export async function getUserMealPlans(userId: ID): Promise<MealPlanType[]> {
  await MongoDBClient();
  
  const mealPlans = await MealPlan.find({ user: userId })
    .populate({
      path: "recipes.recipe",
      model: "Recipe"
    })
    .sort({ date: 1 });
  
  return mealPlans;
}

export async function getWeeklyMealPlans(userId: ID, date = new Date()): Promise<MealPlanType[]> {
  await MongoDBClient();
  
  const startDate = startOfWeek(date, { weekStartsOn: 1 });
  const endDate = endOfWeek(date, { weekStartsOn: 1 });

  await Recipe.find();  // FIXME: This is a dummy line to fix the unregeistered model error
  
  const mealPlans = await MealPlan.find({
    user: userId,
    date: {
      $gte: startDate,
      $lte: endDate
    }
  })
  .populate({
    path: "recipes.recipe",
    model: "Recipe"
  })
  .sort({ date: 1 });
  
  return mealPlans;
}

export async function addMealPlan(mealPlanInput: MealPlanInput, userId?: ID): Promise<MealPlanType> {
  await MongoDBClient();

  // Get the user from session if no userId is provided
  if (!userId) {
    const user = await getUserServer();
    userId = user._id;
  }
  
  // Calculate total macros
  const totalMacros: Macros = {
    calories: 0,
    protein: 0,
    carbs: 0,
    fat: 0,
    fiber: 0
  };
  
  // Create recipe objects with just the _id for saving to database
  const recipeData = mealPlanInput.recipes.map(item => {
    // If recipe is just an ID reference (simplified from client), use it directly
    const recipeId = typeof item.recipe === 'object' && '_id' in item.recipe 
      ? item.recipe._id 
      : (item.recipe as any)._id;
      
    return {
      recipe: recipeId,
      servings: item.servings,
      mealTime: item.mealTime
    };
  });
  
  // If we have complete recipe objects, calculate macros
  if (mealPlanInput.recipes.some(item => typeof item.recipe === 'object' && 'totalMacros' in item.recipe)) {
    for (const recipeItem of mealPlanInput.recipes) {
      if (typeof recipeItem.recipe === 'object' && 'totalMacros' in recipeItem.recipe) {
        const recipe = recipeItem.recipe as RecipeType;
        const servingRatio = recipeItem.servings / recipe.servings;
        
        totalMacros.calories += recipe.totalMacros.calories * servingRatio;
        totalMacros.protein += recipe.totalMacros.protein * servingRatio;
        totalMacros.carbs += recipe.totalMacros.carbs * servingRatio;
        totalMacros.fat += recipe.totalMacros.fat * servingRatio;
        totalMacros.fiber += recipe.totalMacros.fiber * servingRatio;
      }
    }
  }
  
  const mealPlan = await MealPlan.create({
    date: mealPlanInput.date,
    recipes: recipeData,
    totalMacros,
    user: userId
  });
  
  return mealPlan;
}

export async function generateShoppingList(userId?: ID, date = new Date()): Promise<ShoppingListItem[]> {
  await MongoDBClient();
  
  // Get the user from session if no userId is provided
  if (!userId) {
    const user = await getUserServer();
    userId = user._id;
  }
  
  const startDate = startOfWeek(date, { weekStartsOn: 1 });
  const endDate = endOfWeek(date, { weekStartsOn: 1 });
  
  // Get all meal plans for the week
  const mealPlans = await MealPlan.find({
    user: userId,
    date: {
      $gte: startDate,
      $lte: endDate
    }
  })
  .populate({
    path: "recipes.recipe",
    model: "Recipe",
    populate: {
      path: "ingredients.ingredient"
      // Note: refPath doesn't work with older mongoose versions
      // We'll handle both ingredient and recipe types in the processing code
    }
  });
  
  // Aggregate ingredients needed
  const ingredientsMap = new Map<string, ShoppingListItem>();
  
  for (const mealPlan of mealPlans) {
    for (const recipeItem of mealPlan.recipes) {
      const recipe = recipeItem.recipe as RecipeType;
      const servingRatio = recipeItem.servings / recipe.servings;
      
      // Process each ingredient in the recipe
      for (const ingredientItem of recipe.ingredients) {
        // Skip if it's a sub-recipe (process those ingredients separately)
        // Use type assertions to avoid TypeScript errors
        const isSubRecipe = 
          (typeof ingredientItem.isRecipe === 'string' && ingredientItem.isRecipe === 'Recipe') || 
          (typeof ingredientItem.isRecipe === 'boolean' && ingredientItem.isRecipe === true);
        
        if (isSubRecipe) {
          // In a real implementation, we would recursively process sub-recipes
          // For now, we'll skip sub-recipes to keep things simpler
          continue;
        }
        
        const ingredient = ingredientItem.ingredient;
        
        // Skip if ingredient is not properly loaded
        if (!ingredient) continue;

        // Safe way to access ingredient properties
        const ingredientName = typeof ingredient.name === 'string' ? ingredient.name : 'Unknown Ingredient';
        const ingredientId = ingredient._id ? ingredient._id.toString() : `unknown-${Math.random()}`;
        const amount = ingredientItem.amount * servingRatio;
        // Use a type assertion to handle category
        const category = typeof (ingredient as any).category === 'string' 
          ? (ingredient as any).category 
          : 'Other';
        
        const key = `${ingredientId}-${ingredientItem.unit}`;
        
        if (ingredientsMap.has(key)) {
          const existingItem = ingredientsMap.get(key)!;
          existingItem.amount += amount;
        } else {
          ingredientsMap.set(key, {
            ingredient: ingredientName,
            amount,
            unit: ingredientItem.unit,
            category
          });
        }
      }
    }
  }
  
  // Convert to array and sort by category and name
  return Array.from(ingredientsMap.values())
    .sort((a, b) => {
      if (a.category !== b.category) {
        return a.category.localeCompare(b.category);
      }
      return a.ingredient.localeCompare(b.ingredient);
    });
}

export async function deleteMealPlan(mealPlanId: ID): Promise<void> {
  await MongoDBClient();
  await MealPlan.findByIdAndDelete(mealPlanId);
}
