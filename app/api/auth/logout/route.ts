
import { revalidatePath } from "next/cache";
import { NextRequest, NextResponse } from "next/server";

export async function GET(request: NextRequest) {
	try {
		const callbackUrl = request.nextUrl.searchParams.get("callbackUrl");

		const url = new URL(callbackUrl || "/", request.nextUrl);

		const response = NextResponse.redirect(url);		

		response.cookies.set("auth_token", "", { httpOnly: true, expires: new Date(0)})

        revalidatePath("/");
		return response;
		
	} catch (error : any) {
		return NextResponse.json({ error: error.message }, { status: 500 });
	}
}
