"use server";

import { FormState, fromErrorToFormState } from "@/helper/FormErrorHandler";
import { toFormState } from "@/helper/toFormState";

import bcryptjs from "bcryptjs";

import MongoDBClient from "@/mongo/client";
import GoalModel from "@/model/goal";

import { z } from "zod";
import User from "@/model/user";
import UserType, { UserInput, UserOutput } from "@/types/user";
import { sendEmail } from "@/helper/mailer";
import { ID } from "@/types/id";


export async function getAllUser(searchUser: string = "", sortBy?: string): Promise<UserType[]> {
	await MongoDBClient();
	
	const users = await User.find({
		name: {
			$regex: new RegExp(searchUser, "i"),
		},
	});
	return users;
}

export async function getUserById(id: ID): Promise<UserOutput | null> {
	await MongoDBClient();

	const user = await User.findById(id).select("-password");
	return user;
}

export async function getFullUserById(id: ID): Promise<UserType | null> {
    await MongoDBClient();

    const user = await User.findById(id);
    return user;
}

const createUserSchema = z.object({
	email: z.string().email(),
	image: z.string().optional(),
	password: z.string().min(8),
	confirmPassword: z.string().min(8),
})
export async function addUser(formState: FormState, user: FormData): Promise<FormState> {
	try {
		const userData = {
			email: user.get("email"),
			image: user.get("userImage"),
			password: user.get("password"),
			confirmPassword: user.get("confirmPassword"),
		};

		const validatedData = createUserSchema.parse(userData);

		// Check if password and confirmPassword are the same
		if (validatedData.password !== validatedData.confirmPassword) {
			return {
				status: "ERROR",
				message: "Password and Confirm Password must be the same",
				fieldErrors: {
					password: ["Password and Confirm Password must be the same"],
					confirmPassword: ["Password and Confirm Password must be the same"],
				},
				timestamp: Date.now(),
				fieldValues: userData
			}
		}

		await MongoDBClient();

		const existingUser = await User.findOne({
			email: validatedData.email,
		});

		if (existingUser) {
			return {
				status: "ERROR",
				message: "Email already taken",
				fieldErrors: {
					email: ["Email already taken"],
				},
				timestamp: Date.now(),
				fieldValues: userData,
			}
		}
		const salt = await bcryptjs.genSalt(10)
		const hashedPassword = await bcryptjs.hash(validatedData.password, salt)

        const goal = await GoalModel.findOne({ name: "Default" });

		const newUser = await User.create({
			email: validatedData.email,
			image: validatedData.image,
			password: hashedPassword,
            goal: goal,
		});

		try {
			await sendEmail({ email: validatedData.email, emailType: "VERIFY", userId: newUser._id });
		} catch (error) {
			await User.deleteOne({ _id: newUser._id });
			return {
				status: "ERROR",
				message: "Unable to send verification email",
				timestamp: Date.now(),
				fieldValues: userData,
				fieldErrors: {
					email: ["Unable to send verification email"],
				},
			}
		}

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

export async function createUser(user: UserInput): Promise<UserType> {
	await MongoDBClient();

    const goal = await GoalModel.findOne({ name: "Default" });

	const newUser = await User.create({
        ...user,
        goal: goal,
    });

	return newUser;
}
