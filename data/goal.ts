"use server";

import { FormState, fromErrorToFormState } from "@/helper/FormErrorHandler";
import { toFormState } from "@/helper/toFormState";
import GoalModel from "@/model/goal";
import MongoDBClient from "@/mongo/client";
import Goal from "@/types/goal";
import { revalidatePath } from "next/cache";
import { z } from "zod";

export async function getCurrentGoal(): Promise<Goal> {
	await MongoDBClient();

	const goal = await GoalModel.findOne({});
	if (!goal) {
		// Create default goal
		const ketoGoal = {
			name: "High-Protein Keto",
			targetMacro: {
				calories: 1700,
				protein: 150,
				carbs: 22,
				fat: 115,
				fiber: 30,
			},
		}

		return await GoalModel.create(ketoGoal);
	}
	return goal;
}

const createGoalSchema = z.object({
	name: z.string().min(1).max(100),
	targetMacro: z.object({
		calories: z.number().min(0),
		protein: z.number().min(0),
		carbs: z.number().min(0),
		fat: z.number().min(0),
		fiber: z.number().min(0),
	}),
});

export async function createGoal(formState: FormState, formData: FormData): Promise<FormState> {
	try {
		const goalData = {
			name: formData.get("name") as string,
			targetMacro: {
				calories: Number(formData.get("calories")),
				protein: Number(formData.get("protein")),
				carbs: Number(formData.get("carbs")),
				fat: Number(formData.get("fat")),
				fiber: Number(formData.get("fiber")),
			},
		}

		const validatedData = createGoalSchema.parse(goalData);

		await MongoDBClient();

		// Create new goal entry
		const newGoal = await GoalModel.create({
			name: validatedData.name,
			targetMacro: validatedData.targetMacro,
		});

		if (!newGoal) {
			throw new Error("Failed to create goal");
		}
	} catch (error) {
		return fromErrorToFormState(error);
	}

	revalidatePath("/goal");

	return toFormState("SUCCESS", "Meal added successfully!");
}
