"use server";

import { ApiError, request, type ApiRequestOptions } from "@/lib/api";
import { serverApiOrigin, sessionCookieHeader } from "@/lib/auth";

/**
 * Server-side calls forward the caller's session cookie. Nothing here mints or
 * caches a token any more - see docs/REFOCUS.md §6.
 */
export async function serverApi<T>(
	path: string,
	options: ApiRequestOptions = {},
): Promise<T> {
	const cookie = await sessionCookieHeader();
	const requireAuth = options.requireAuth !== false;

	if (requireAuth && !cookie) {
		throw new ApiError(401, "Unauthorized", { error: "Not authenticated" });
	}

	return request<T>(
		serverApiOrigin(),
		path,
		cookie ? { mode: "cookie", cookie } : { mode: "none" },
		options,
	);
}
