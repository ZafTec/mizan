
import Goal, { GoalVersion } from "@/types/goal";
import mongoose, { Document, model, Schema, Types } from "mongoose";


export interface IGoalVersion extends Document, GoalVersion {
	_id: Schema.Types.ObjectId;
}

const GoalVersionSchema = new Schema<IGoalVersion>({
	name: { type: String, required: true },
	targetMacro: {
		calories: { type: Number, required: true, default: 0 },
		protein: { type: Number, required: true, default: 0 },
		carbs: { type: Number, required: true, default: 0 },
		fat: { type: Number, required: true, default: 0 },
		fiber: { type: Number, required: true, default: 0 },
	},
}, { timestamps: true });


export interface IGoal extends Document, Goal {
	_id: Schema.Types.ObjectId;
	versions: Types.Array<IGoalVersion>;
}

const GoalSchema = new Schema<IGoal>({
	versions: [GoalVersionSchema],
}, { timestamps: true });

export default mongoose.models.Goal || model<IGoal>("Goal", GoalSchema);
