
import Goal from "@/types/goal";
import mongoose, { Document, model, Schema } from "mongoose";

export interface IGoal extends Document, Goal {
    _id: Schema.Types.ObjectId;
}

const GoalSchema = new Schema<IGoal>({
    name: { type: String, required: true },
    targetMacro: {
        calories: { type: Number, required: true, default: 0 },
        protein: { type: Number, required: true, default: 0 },
        carbs: { type: Number, required: true, default: 0 },
        fat: { type: Number, required: true, default: 0 },
        fiber: { type: Number, required: true, default: 0 },
    },
}, { timestamps: true });

export default mongoose.models.Goal || model<IGoal>("Goal", GoalSchema);
