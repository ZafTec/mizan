import mongoose, { Document, model, Schema } from "mongoose";
import Suggestion from "@/types/suggestion";

export interface ISuggestion extends Document, Suggestion {
  _id: Schema.Types.ObjectId;
}

const SuggestionSchema = new Schema<ISuggestion>({
  user: { type: Schema.Types.ObjectId, ref: "User", required: true },
  recipes: [{ type: Schema.Types.ObjectId, ref: "Recipe" }],
}, { timestamps: true });

export default mongoose.models.Suggestion || model<ISuggestion>("Suggestion", SuggestionSchema);
