import Macros from "./macro";

export default interface Ingredient {
    id: string | unknown;
	name: string;
	servingSize: number;
	servingUnit: string;
	macros: Macros;
	verified: boolean;  // for system vs user-added ingredients
}
