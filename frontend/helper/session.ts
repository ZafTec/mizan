import { getCurrentUser } from "@/lib/auth";

/** The signed-in user, or null. */
export async function getUserOptionalServer() {
	return getCurrentUser();
}

/** The signed-in user. Throws when there isn't one - use in protected routes. */
export async function getUserServer() {
	const user = await getCurrentUser();
	if (!user) throw new Error("Not authenticated");
	return user;
}
