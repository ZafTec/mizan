"use server";

import { logger } from "@/lib/logger";
import { ApiError, request, type ApiRequestOptions } from "@/lib/api";

const apiLogger = logger.createModuleLogger("api.server");

export async function serverApi<T>(
  path: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const { headers: headersFn } = await import("next/headers");
  const { auth } = await import("@/lib/auth");

  const requestHeaders = await headersFn();
  const session = await auth.api.getSession({ headers: requestHeaders });
  const requireAuth = options.requireAuth !== false;

  if (requireAuth && !session?.user?.id) {
    throw new ApiError(401, "Unauthorized", { error: "Not authenticated" });
  }

  const baseUrl =
    process.env.API_URL || process.env.BACKEND_API_URL || "http://backend:8080";
  const token = await getServerToken(requestHeaders);

  return request<T>(baseUrl, path, token, options);
}

async function getServerToken(headers: Headers): Promise<string | null> {
  const baseUrl =
    process.env.BETTER_AUTH_URL || buildBaseUrlFromHeaders(headers);
  if (!baseUrl) {
    apiLogger.error("Unable to determine BetterAuth base URL for token");
    return null;
  }

  try {
    const response = await fetch(`${baseUrl}/api/auth/token`, {
      method: "GET",
      headers: { cookie: headers.get("cookie") ?? "" },
    });

    if (!response.ok) {
      apiLogger.warn("Failed to fetch server API token", {
        status: response.status,
      });
      return null;
    }

    const data = await response.json().catch(() => null);
    return data?.token ?? null;
  } catch (error) {
    apiLogger.error("Server API token fetch failed", {
      error: error instanceof Error ? error.message : String(error),
    });
    return null;
  }
}

function buildBaseUrlFromHeaders(headers: Headers): string | null {
  const host = headers.get("x-forwarded-host") ?? headers.get("host");
  if (!host) return null;
  const protocol = headers.get("x-forwarded-proto") ?? "http";
  return `${protocol}://${host}`;
}
