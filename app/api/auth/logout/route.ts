
import { NextRequest, NextResponse } from "next/server";

export async function GET(request: NextRequest) {
	try {
		const callbackUrl = request.nextUrl.searchParams.get("callbackUrl") || "/";

		// const url = new URL(callbackUrl || "/", request.nextUrl);

		const response = NextResponse.json({ message: "Logged out successfully", callbackUrl }, { status: 200 });		

		response.cookies.set("auth_token", "", { httpOnly: true, expires: new Date(0)})


		return response;
		
	} catch (error : any) {
		return NextResponse.json({ error: error.message }, { status: 500 });
	}
}
