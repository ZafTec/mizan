import Recipe from "@/types/recipe";
import mongoose from "mongoose";
import { Document, model, Schema } from "mongoose";

export interface IRecipe extends Document, Recipe {
	id: Schema.Types.ObjectId;
}


const RecipeSchema = new Schema<IRecipe>({
	userId: { type: Schema.Types.ObjectId, required: true },
	name: { type: String, required: true },
	description: { type: String },
	ingredients: [{
		ingredient: { type: Schema.Types.ObjectId, ref: "Ingredient" },
		amount: { type: Number, required: true },
		unit: { type: String, required: true },
	}],
	totalMacros: {
		calories: { type: Number, required: true, default: 0 },
		protein: { type: Number, required: true, default: 0 },
		carbs: { type: Number, required: true, default: 0 },
		fat: { type: Number, required: true, default: 0 },
		fiber: { type: Number, required: true, default: 0 },
	},
	servings: { type: Number, required: true, default: 1 },
	instructions: { type: [String], required: true },
	tags: { type: [String], required: true },
}, { timestamps: true});

export default mongoose.models.Recipe || model<IRecipe>("Recipe", RecipeSchema);
