import Recipe from "@/types/recipe";
import mongoose from "mongoose";
import { Document, model, Schema } from "mongoose";

export interface IRecipe extends Document, Recipe {
	_id: Schema.Types.ObjectId;
}


const RecipeSchema = new Schema<IRecipe>({
	name: { type: String, required: true },
	images: { type: [String], required: true, default: [] },
	description: { type: String },
	ingredients: [{
		ingredient: { 
			type: Schema.Types.ObjectId, 
			refPath: 'ingredients.isRecipe' 
		},
		amount: { type: Number, required: true },
		unit: { type: String, required: true },
		isRecipe: { 
			type: String, 
			required: false, 
			enum: ['Ingredient', 'Recipe'],
			default: 'Ingredient'
		}
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
    creator: { type: Schema.Types.ObjectId, ref: "User" },
}, { timestamps: true});

export default mongoose.models.Recipe || model<IRecipe>("Recipe", RecipeSchema);
