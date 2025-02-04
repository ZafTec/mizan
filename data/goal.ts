"use server";

import { FormState, fromErrorToFormState } from "@/helper/FormErrorHandler";
import { getUserServer } from "@/helper/session";
import { toFormState } from "@/helper/toFormState";
import GoalModel from "@/model/goal";
import UserModel from "@/model/user";
import MongoDBClient from "@/mongo/client";
import Goal, { GoalVersion } from "@/types/goal";
import { ID } from "@/types/id";
import User from "@/types/user";
import { revalidatePath } from "next/cache";
import { z } from "zod";


export async function createDefaultGoal(): Promise<Goal> {
	await MongoDBClient();

	const defaultGoal = await GoalModel.create({
		versions: [
			{
				name: "Default",
				targetMacro: {
					calories: 2000,
					protein: 150,
					carbs: 250,
					fat: 70,
					fiber: 30,
				},
			},
		],
	});

	if (!defaultGoal) {
		throw new Error("Failed to create default goal");
	}

	return defaultGoal;
}


export async function getCurrentGoal(userId: ID): Promise<GoalVersion> {
	await MongoDBClient();

	const user = await UserModel.findById(userId).populate("goal") as User;
	const goal = user.goal as Goal;
	// sort the versions by createdAt and get the latest version
	goal.versions.sort((a, b) => b.createdAt.getTime() - a.createdAt.getTime());
	
	if (!goal || goal.versions.length === 0) {
		throw new Error("Goal not found");
	}
	return goal.versions[0];
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
	const user = await getUserServer();
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
		const goalID = user.goal._id as ID;
		let newGoal;
		if (!goalID) {
			newGoal = await GoalModel.create({
				versions: [
					{
						name: validatedData.name,
						targetMacro: validatedData.targetMacro,
					},
				],
			});

			await UserModel.findByIdAndUpdate(user._id, {
				goal: newGoal._id,
			});
		} else {
			newGoal = await GoalModel.findByIdAndUpdate(goalID, {
				$push: {
					versions: {
						name: validatedData.name,
						targetMacro: validatedData.targetMacro,
					},
				},
			});
		}

		if (!newGoal) {
			throw new Error("Failed to create goal");
		}
	} catch (error) {
		// TODO: Remove all the created data
		return fromErrorToFormState(error);
	}

	revalidatePath("/goal");

	return toFormState("SUCCESS", "Meal added successfully!");
}
