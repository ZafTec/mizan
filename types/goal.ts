
import { ObjectId } from "mongoose";
import Macros from "./macro";

export default interface Goal {
	_id: string | ObjectId;

	name: string;
	targetMacro: Macros;

	// timestamps
	createdAt: Date;
	updatedAt: Date;
}
