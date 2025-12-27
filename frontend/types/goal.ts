
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
