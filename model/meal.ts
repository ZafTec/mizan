
import Meal from "@/types/meal";
import mongoose from "mongoose";
import { Document, model, Schema } from "mongoose";

export interface IMeal extends Document, Meal {
    id: Schema.Types.ObjectId;
}

const MealSchema = new Schema<IMeal>({
    name: { type: String, required: true },
    mealType: { type: String, required: true },
    totalMacros: {
        calories: { type: Number, required: true, default: 0 },
        protein: { type: Number, required: true, default: 0 },
        carbs: { type: Number, required: true, default: 0 },
        fat: { type: Number, required: true, default: 0 },
        fiber: { type: Number, required: true, default: 0 },
    },
}, { timestamps: true });

export default mongoose.models.Meal || model<IMeal>("Meal", MealSchema);
