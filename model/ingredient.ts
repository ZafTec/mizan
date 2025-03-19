import Ingredient from "@/types/ingredient";
import mongoose, { Document, Schema, model } from "mongoose";

export interface IIngredient extends Document, Ingredient {
	_id: Schema.Types.ObjectId;
}

const IngredientSchema = new Schema<IIngredient>({
	name: { type: String, required: true },
	servingSize: { type: Number, required: true, min: 0, default: 1 },
	servingUnit: { type: String, required: true, default: "g" },
	macros: {
		calories: { type: Number, required: true, min: 0, default: 0 },
		protein: { type: Number, required: true, min: 0, default: 0 },
		carbs: { type: Number, required: true, min: 0, default: 0 },
		fat: { type: Number, required: true, min: 0, default: 0 },
		fiber: { type: Number, required: true, min: 0, default: 0 },
	},
	verified: { type: Boolean, required: true, default: false },
}, { timestamps: true });

export default mongoose.models.Ingredient || model<IIngredient>("Ingredient", IngredientSchema);
