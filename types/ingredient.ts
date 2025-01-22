import Macros from "./macro";
import { ID } from "./id";

export default interface Ingredient {
    _id: ID;
	name: string;
	servingSize: number;
	servingUnit: string;
	macros: Macros;
	verified: boolean;  // for system vs user-added ingredients

    // timestamps
    createdAt: string;
    updatedAt: string;
}
