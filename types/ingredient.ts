import { ObjectId } from "mongoose";
import Macros from "./macro";

export default interface Ingredient {
    _id: string | ObjectId;
	name: string;
	servingSize: number;
	servingUnit: string;
	macros: Macros;
	verified: boolean;  // for system vs user-added ingredients

    // timestamps
    createdAt: string;
    updatedAt: string;
}
