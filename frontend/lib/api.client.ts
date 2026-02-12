import { logger } from "@/lib/logger";
import { ApiError, request, type ApiRequestOptions } from "@/lib/api";

const apiLogger = logger.createModuleLogger("api.client");

let cachedToken: { token: string; expiresAt: number } | null = null;

function decodeJwtExpiry(token: string): number | null {
  try {
    const payload = token.split(".")[1];
    if (!payload) return null;
    const decoded = JSON.parse(atob(payload));
    return decoded?.exp ? decoded.exp * 1000 : null;
  } catch {
    return null;
  }
}

export async function getApiToken(): Promise<string | null> {
  if (cachedToken && cachedToken.expiresAt > Date.now() + 30000) {
    return cachedToken.token;
  }

  try {
    const response = await fetch("/api/auth/token", {
      method: "GET",
      credentials: "include",
    });

    if (!response.ok) {
      apiLogger.warn("Failed to fetch API token", { status: response.status });
      return null;
    }

    const data = await response.json().catch(() => null);
    const token = data?.token;
    if (!token) {
      apiLogger.warn("API token missing in response");
      return null;
    }

    const expiresAt = decodeJwtExpiry(token) ?? Date.now() + 10 * 60 * 1000;
    cachedToken = { token, expiresAt };
    return token;
  } catch (error) {
    apiLogger.error("API token fetch failed", {
      error: error instanceof Error ? error.message : String(error),
    });
    return null;
  }
}

async function handleSessionExpired(): Promise<void> {
  if (typeof window === "undefined") return;

  const currentPath = window.location.pathname;
  apiLogger.warn("Session expired", { currentPath });

  try {
    const { authClient } = await import("@/lib/auth-client");
    await authClient.signOut({ fetchOptions: { onSuccess: () => {} } });
  } catch (error) {
    apiLogger.error("Error signing out", {
      error: error instanceof Error ? error.message : String(error),
    });
  }

  if (currentPath !== "/login" && currentPath !== "/signup") {
    window.location.href = `/login?error=session_expired&redirect=${encodeURIComponent(currentPath)}`;
  }
}

export async function clientApi<T>(
  path: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const apiUrl = process.env["NEXT_PUBLIC_API_URL"]!;
  const token = await getApiToken();

  try {
    return await request<T>(apiUrl, path, token, options);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      await handleSessionExpired();
      throw new Error("Session expired. Please log in again.");
    }
    throw error;
  }
}
