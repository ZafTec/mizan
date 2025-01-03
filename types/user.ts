import { ObjectId } from "mongoose";

export default interface User {
    id: string | ObjectId;
	email: string;
	name?: string;
	image?: string;
	password?: string;
	// preferences?: UserPreferences;
}

  