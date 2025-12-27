// User type compatible with BetterAuth
export default interface User {
	id: string;
	email: string;
	name: string;
	emailVerified: boolean;
	image?: string | null;
	createdAt: Date;
	updatedAt: Date;
}

export interface UserInput {
	email: string;
	image?: string;
	password: string;
}

export interface UserOutput {
	email: string;
	image?: string;
}
  