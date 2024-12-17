import User from "@/types/user";
import { Document, model, Schema } from "mongoose";


export interface IUser extends Document, User {
	id: Schema.Types.ObjectId;
}

const UserSchema = new Schema<IUser>({
	email: { type: String, required: true },
	name: { type: String, required: true },
	image: { type: String },
	password: { type: String },
});

export default model<IUser>("User", UserSchema);
