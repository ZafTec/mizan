
import { getNutritionOverview } from "@/data/meal";
import { getUserById } from "@/data/user";
import { getDataFromToken } from "@/helper/getDataFromToken";
import { NextRequest, NextResponse } from "next/server";


export async function GET(request: NextRequest){
	try {
		// Extract user ID from the authentication token
		const userId = getDataFromToken(request);

		// Find the user in the database based on the user ID
		const user = await getUserById(userId);

		if (!user) {
			return NextResponse.json({ error: "User not found" }, { status: 404 });
		}
		
        const macros = await getNutritionOverview(userId);

        return NextResponse.json({ user, macros }, { status: 200 });
	} catch (error: any) {
		return NextResponse.json({ serverError: error.message, error: error.message }, { status: 400 })
	}
}
