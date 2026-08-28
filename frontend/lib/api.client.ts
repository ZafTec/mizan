import { logger } from "@/lib/logger";
import { ApiError, request, type ApiRequestOptions } from "@/lib/api";
import { resolvePublicApiOrigin } from "@/lib/api-base";

const apiLogger = logger.createModuleLogger("api.client");

async function handleSessionExpired(): Promise<void> {
  if (typeof window === "undefined") return;

  const currentPath = window.location.pathname;
  apiLogger.warn("Session expired", { currentPath });

  if (currentPath !== "/login" && currentPath !== "/register") {
    window.location.href = `/login?error=session_expired&redirect=${encodeURIComponent(currentPath)}`;
  }
}

/**
 * Browser calls carry the session cookie and nothing else. The token cache,
 * the /api/auth/token round trip and the refresh dance are all gone with
 * BetterAuth - see docs/REFOCUS.md §6.
 */
export async function clientApi<T>(
  path: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  try {
    return await request<T>(resolvePublicApiOrigin(), path, { mode: "browser" }, options);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      await handleSessionExpired();
      throw new Error("Session expired. Please log in again.");
    }
    throw error;
  }
}
