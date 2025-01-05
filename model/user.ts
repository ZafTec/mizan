import User from "@/types/user";
import mongoose, { Document, model, Schema } from "mongoose";


export interface IUser extends Document, User {
	id: Schema.Types.ObjectId;
}

const UserSchema = new Schema<IUser>({
	email: { type: String, unique: true, required: true },
	name: { type: String, required: true },
	image: { type: String },
	password: { type: String },
});

export default mongoose.models.User || model<IUser>("User", UserSchema);
