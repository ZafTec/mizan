import User from "@/types/user";
import mongoose, { Document, model, Schema } from "mongoose";


export interface IUser extends Document, User {
	_id: Schema.Types.ObjectId;
}

const UserSchema = new Schema<IUser>({
	email: { type: String, unique: true, required: true },
	image: { type: String },
	password: { type: String },
	isVerified: {
		type: Boolean,
		default: false,
	},
	isAdmin: {
		 type: Boolean,
		 default: false,
	},
	forgotPasswordToken: {
		type: String,
		nullable: true,
	},
	forgotPasswordTokenExpiry: {
		type: Date,
		nullable: true,
	},
	verifyToken: {
		type: String,
		nullable: true,
	},
	verifyTokenExpiry: {
		type: Date,
		nullable: true,
	},
	goal: { type: Schema.Types.ObjectId, ref: "Goal", default: null },
}, { timestamps: true });

export default mongoose.models.User || model<IUser>("User", UserSchema);
