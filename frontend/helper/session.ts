import { auth } from "@/lib/auth";
import { headers } from "next/headers";

/**
 * Get the current user session on the server side.
 * Returns null if not authenticated.
 */
export async function getUserOptionalServer() {
    try {
        const session = await auth.api.getSession({
            headers: await headers(),
        });
        return session?.user ?? null;
    } catch (error) {
        console.error("Failed to get session:", error);
        return null;
    }
}

/**
 * Get the current user session on the server side.
 * Throws an error if not authenticated.
 */
export async function getUserServer() {
    const user = await getUserOptionalServer();
    if (!user) {
        throw new Error("Not authenticated");
    }
    return user;
}
