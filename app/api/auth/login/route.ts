import { getUserByEmail } from "@/data/user";
import { NextRequest, NextResponse } from "next/server";
import bcryptjs from "bcryptjs";
import jwt from "jsonwebtoken";


export async function POST(request: NextRequest) {
	try {
		const body = await request.json();
		const { email, password } = body;
		const callbackUrl = request.nextUrl.searchParams.get("callbackUrl");

		const user = await getUserByEmail(email);

		if(!user){
			return NextResponse.json({error: "User does not exist"}, {status: 400})
		}
		
		//check if password is correct
		const validPassword = await bcryptjs.compare(password, user.password)

		if(!validPassword){
			return NextResponse.json({error: "Invalid password"}, {status: 400})
		}

		const tokenData = {
			id: user._id,
		}

		const token = jwt.sign(tokenData, process.env.TOKEN_SECRET!, {expiresIn: "1d"});
		
		const response = NextResponse.json({
			message: "Login successful",
			success: true,
			callbackUrl: callbackUrl || "/",
		})

		response.cookies.set("auth_token", token, {
			httpOnly: true,
			maxAge: 60 * 60 * 24,  // 1 day
		})

		return response;
	} catch (error: any) {
		return NextResponse.json({ serverError: error.message, error: "Unable to handle request" }, { status: 500 });
	}
}
