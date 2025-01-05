"use server";

import { FormState, fromErrorToFormState } from "@/helper/FormErrorHandler";
import { toFormState } from "@/helper/toFormState";

import MongoDBClient from "@/mongo/client";

import { z } from "zod";
import User from "@/model/user";
import UserType from "@/types/user";


export async function getAllUser(searchUser: string = "", sortBy?: string): Promise<UserType[]> {
	await MongoDBClient();
	
	const users = await User.find({
		name: {
			$regex: new RegExp(searchUser, "i"),
		},
	});
	return users;
}

export async function getUserById(id: string): Promise<UserType | null> {
	await MongoDBClient();

	const user = await User.findById(id);
	return user;
}

const createUserSchema = z.object({
	email: z.string().email(),
	name: z.string().min(1).max(100),
	image: z.string().optional(),
	password: z.string().min(8),
	confirmPassword: z.string().min(8),
	// password and confirmPassword should be equal
}).refine(data => data.password === data.confirmPassword, {
	message: "Passwords do not match",
})

export async function addUser(formState: FormState, user: FormData): Promise<FormState> {
	try {
		const userData = {
			email: user.get("email"),
			name: user.get("name"),
			image: user.get("userImage"),
			password: user.get("password"),
			confirmPassword: user.get("confirmPassword"),
		};
		console.log(userData);

		const validatedData = createUserSchema.parse(userData);
		console.log(validatedData);

		await MongoDBClient();

		const newUser = await User.create({
			email: validatedData.email,
			name: validatedData.name,
			image: validatedData.image,
			password: validatedData.password,
		});

		return toFormState("SUCCESS", newUser.id.toString());
	} catch (error) {
		return fromErrorToFormState(error);
	}
}

export async function getUserByEmail(email: string): Promise<UserType | null> {
	await MongoDBClient();

	const user = await User.findOne({ email });

	return user;
}
