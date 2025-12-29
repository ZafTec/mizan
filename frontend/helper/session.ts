import { auth } from "@/lib/auth";
import { headers } from "next/headers";
import { logger } from "@/lib/logger";

const sessionLogger = logger.createModuleLogger("session-helper");

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
        sessionLogger.error("Failed to get user session", { error });
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
        sessionLogger.error("Attempted to get user server but not authenticated");
        throw new Error("Not authenticated");
    }
    return user;
}
