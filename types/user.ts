
import Goal from "./goal";
import { ID } from "./id";

export default interface User {
	_id: ID;
	email: string;
	image?: string;
	password: string;

	isVerified: Boolean;
	isAdmin: Boolean;

	forgotPasswordToken: String | null;
	forgotPasswordTokenExpiry: Date | null;
	verifyToken: String | null;
	verifyTokenExpiry: Date | null;

    goal: Goal;

	// timestamps
	createdAt: Date;
	updatedAt: Date;
}

export interface UserInput {
	email: string;
	image?: string;
	password: string;
}

export interface UserOutput {
	email: string;
	image?: string;
}
  