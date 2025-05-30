
import { z } from 'zod';
import Ingredient from './ingredient';
import { GoalVersion } from './goal';
import MealType from './meal';
import Recipe from './recipe';


export const RecipeSchema = z.object({
    name: z.string(),
    description: z.string(),
    ingredients: z.array(
        z.object({
            id: z.string(),
            name: z.string(),
            amount: z.number(),
        })
    ),
    instructions: z.array(z.string()),
    fromSystem: z.object({
        id: z.string()
    }).nullable()
});
export const RecipesSchema = z.object({
    recipes: z.array(
        RecipeSchema
    ),
    totalMacros: z.object({
        calories: z.number(),
        protein: z.number(),
        carbohydrates: z.number(),
        fat: z.number(),
        fiber: z.number().default(0)
    })
});

export type OpenAIRecipesResponseSchema = z.infer<typeof RecipesSchema>;

export interface OpenAIChatInput {
    ingredients: Ingredient[];
    currentGoal: GoalVersion;
    mealsConsumedToday: MealType[];
    recipesInSystem: Recipe[];
}
