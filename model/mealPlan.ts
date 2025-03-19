import mongoose from "mongoose";
import { Document, model, Schema } from "mongoose";
import MealPlan from "@/types/mealPlan";

export interface IMealPlan extends Document, MealPlan {
  _id: Schema.Types.ObjectId;
}

const MealPlanSchema = new Schema<IMealPlan>({
  date: { type: Date, required: true },
  recipes: [{
    recipe: { type: Schema.Types.ObjectId, ref: "Recipe", required: true },
    servings: { type: Number, required: true, default: 1 },
    mealTime: { type: String, required: true }
  }],
  totalMacros: {
    calories: { type: Number, required: true, default: 0 },
    protein: { type: Number, required: true, default: 0 },
    carbs: { type: Number, required: true, default: 0 },
    fat: { type: Number, required: true, default: 0 },
    fiber: { type: Number, required: true, default: 0 },
  },
  user: { type: Schema.Types.ObjectId, ref: "User", required: true },
}, { timestamps: true });

export default mongoose.models.MealPlan || model<IMealPlan>("MealPlan", MealPlanSchema); 