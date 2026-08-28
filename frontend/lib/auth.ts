import "server-only";
import { cache } from "react";
import { cookies } from "next/headers";
import { ApiError, request } from "@/lib/api";
import { logger } from "@/lib/logger";

const authLogger = logger.createModuleLogger("auth-server");

export const SESSION_COOKIE = "mizan_session";

/**
 * The shape the backend returns from GET /api/Auth/me. Since v2 the app has no
 * auth library and no auth tables of its own - see docs/REFOCUS.md §6.
 */
export interface User {
	id: string;
	email: string;
	name?: string | null;
	image?: string | null;
	role: string;
	emailVerified: boolean;
	themePreference: string;
	compactMode: boolean;
	reduceAnimations: boolean;
	hasPassword: boolean;

	/** IANA zone. Null until the user has told us; treated as UTC until then. */
	timeZoneId?: string | null;
}

export function serverApiOrigin(): string {
	return process.env.API_URL || process.env.BACKEND_API_URL || "http://backend:8080";
}

/** The raw cookie header to forward to the API, or null when signed out. */
export async function sessionCookieHeader(): Promise<string | null> {
	const token = (await cookies()).get(SESSION_COOKIE)?.value;
	return token ? `${SESSION_COOKIE}=${token}` : null;
}

/**
 * Cached for the render pass: a page and its contextual surfaces all ask who
 * the user is, and that should cost one call, not five.
 */
export const getCurrentUser = cache(async (): Promise<User | null> => {
	const cookie = await sessionCookieHeader();
	if (!cookie) return null;

	try {
		return await request<User>(serverApiOrigin(), "/api/Auth/me", { mode: "cookie", cookie });
	} catch (error) {
		if (error instanceof ApiError && error.status === 401) return null;
		authLogger.error("Failed to resolve session", { error });
		return null;
	}
});
