
import { getServerSession } from "next-auth";
import { options } from "@/app/api/auth/[...nextauth]/options";
import { redirect } from "next/navigation";
 

export default async function getUser() {
	const session = await getServerSession(options);
		
	if (!session) {
		redirect("/api/auth/signin?callbackUrl=/");
	}
	console.log(session);
	const user = session.user;
    return { session, user };
}
