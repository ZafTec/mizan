import { Schema } from "mongoose";

export default interface User {
    _id: string | Schema.Types.ObjectId;
	email: string;
	name: string;
	image?: string;
	password?: string;
	// preferences?: UserPreferences;
}

  