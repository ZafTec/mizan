
import { ID } from '@/types/id';
import jwt from 'jsonwebtoken'
import { NextRequest } from "next/server"

export function getDataFromToken(request: NextRequest): ID {

	try {
		// Retrieve the token from the cookies
		const token = request.cookies.get("auth_token")?.value || '';

		// Verify and decode the token using the secret key
		const decodedToken: any = jwt.verify(token, process.env.TOKEN_SECRET!);

		// Return the user ID from the decoded token
		return decodedToken.id;

	} catch (error: any) {
		throw new Error(error.message)
	}
}
