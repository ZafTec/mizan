import { OpenAIRecipesResponseSchema, OpenAIChatInput, RecipesSchema } from "@/types/openai";
import OpenAI from "openai";
import { zodResponseFormat } from "openai/helpers/zod";
import { logger } from "@/lib/logger";

const openAIChatLogger = logger.createModuleLogger("openai-chat-service");

const openai = new OpenAI();

export async function openAIChatRecipeSuggestions(input: OpenAIChatInput): Promise<OpenAIRecipesResponseSchema> {    	
	// Calculate remaining macros first
	const consumedMacros = input.mealsConsumedToday.reduce(
		(total, meal) => ({
			calories: total.calories + (meal.totalMacros.calories || 0),
			protein: total.protein + (meal.totalMacros.protein || 0),
			carbs: total.carbs + (meal.totalMacros.carbs || 0),
			fat: total.fat + (meal.totalMacros.fat || 0),
		}),
		{ calories: 0, protein: 0, carbs: 0, fat: 0 }
	);

	const remainingMacros = {
		calories: Math.max(0, input.currentGoal.targetMacro.calories - consumedMacros.calories),
		protein: Math.max(0, input.currentGoal.targetMacro.protein - consumedMacros.protein),
		carbs: Math.max(0, input.currentGoal.targetMacro.carbs - consumedMacros.carbs),
		fat: Math.max(0, input.currentGoal.targetMacro.fat - consumedMacros.fat),
	};
	
	const completion = await openai.chat.completions.parse({
		model: "gpt-4.1-mini",
		messages: [
			{
				role: "developer",
				content: `You are a nutrition expert that suggests recipes to help users meet their daily macro targets.

TASK: Suggest recipes using available ingredients that fit within the user's remaining macro targets.

RESPONSE FORMAT:
${JSON.stringify(RecipesSchema.shape, null, 2)}

MACRO CALCULATION RULES:
- Daily target: ${JSON.stringify(input.currentGoal)}
- Already consumed: ${JSON.stringify(consumedMacros)}
- REMAINING TARGET: ${JSON.stringify(remainingMacros)}

RECIPE SELECTION RULES:
1. If remaining macros are (very low), return empty array
2. Use a combination of system recipes and new suggestions(new recipes based on ingredients) to make up remaining macros
3. Prefer fewer accurate recipes over many that exceed targets
4. The total macros of all the suggested recipes must NOT exceed remaining targets
5. Allow 5-10% margin for macro matching
6. Be creative with the recipes, go for diversity and variety
7. Always create a new recipe if the combinations from system recipes do not meet the remaining targets
8. The priorities are calorie and protein, always match those as closely as possible.

INGREDIENT CONSTRAINTS:
- Only use ingredients from the provided list
- Consider realistic portion sizes
- Assume the macros for each ingredient are accurate

	System recipes: ${JSON.stringify(input.recipesInSystem, null, 2)}

	Available ingredients: ${JSON.stringify(input.ingredients)}
				`.trim(),
			},			{
				role: "user",
				content: `
				Please suggest recipes that will help me use my remaining macro targets:
- Remaining calories: ${remainingMacros.calories}
- Remaining protein: ${remainingMacros.protein}g  
- Remaining carbs: ${remainingMacros.carbs}g
- Remaining fat: ${remainingMacros.fat}g

Return recipes that collectively fit within these remaining targets.
				`,
			},
		],
		response_format: zodResponseFormat(RecipesSchema, "recipes"),
	});

	if (completion.choices.length > 0 && completion.choices[0].message?.parsed) {
		const response = completion.choices[0].message.parsed;
		// log the tokens usage
		openAIChatLogger.info("OpenAI Tokens Usage", { usage: completion.usage });
		return RecipesSchema.parse(response);
	}
	
	openAIChatLogger.error("No valid response from OpenAI:", {
		completion: completion ? JSON.stringify(completion) : "No completion"
	});
	throw new Error("No valid response from OpenAI");
}
