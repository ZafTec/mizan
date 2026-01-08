
import Macros from "./macro";
import { ID } from "./id";

export default interface Goal {
	_id: ID;

	versions: GoalVersion[];

	// timestamps
	createdAt: Date;
	updatedAt: Date;
}

export interface GoalVersion {
	_id: ID;
	name: string;
	targetMacro: Macros;

	// timestamps
	createdAt: Date;
	updatedAt: Date;
}

export interface GoalData {
  goal: {
	targetCalories: number;
	targetProteinGrams: number;
	targetCarbsGrams: number;
	targetFatGrams: number;
  } | null;
  progressEntries: Array<{
	id: string;
	date: string;
	actualCalories: number;
	actualProteinGrams: number;
	actualCarbsGrams: number;
	actualFatGrams: number;
	actualWeight?: number;
	notes?: string;
  }>;
}
