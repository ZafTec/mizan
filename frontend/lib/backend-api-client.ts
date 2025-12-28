import "server-only";
import { auth } from "@/lib/auth";
import { logger } from "@/lib/logger";

const bffLogger = logger.createModuleLogger("backend-api-client");

interface BackendApiOptions {
  method?: string;
  body?: any;
  headers?: Record<string, string>;
  requireAuth?: boolean; // Default true for safety
}

export class BackendApiError extends Error {
  constructor(
    public status: number,
    public statusText: string,
    public body: any
  ) {
    super(`Backend API error: ${status} ${statusText}`);
  }
}

/**
 * Server-side API client for calling the backend with trusted secret
 * MUST only be used in Next.js API routes or Server Components/Actions
 */
export async function callBackendApi<T>(
  path: string,
  options: BackendApiOptions = {}
): Promise<T> {
  const session = await auth.api.getSession({ headers: await import("next/headers").then(m => m.headers()) });
  const requireAuth = options.requireAuth !== false; // Default to true

  if (requireAuth && !session?.user?.id) {
    bffLogger.warn("Unauthorized backend API call attempt", { path });
    throw new BackendApiError(401, "Unauthorized", { error: "Not authenticated" });
  }

  const backendUrl = process.env.BACKEND_API_URL || "http://backend:8080";
  const trustedSecret = process.env.BFF_TRUSTED_SECRET;

  if (!trustedSecret) {
    bffLogger.error("BFF_TRUSTED_SECRET not configured");
    throw new Error("BFF_TRUSTED_SECRET not configured");
  }

  const url = `${backendUrl}${path}`;

  bffLogger.debug("Backend API request starting", {
    path,
    method: options.method || "GET",
    userId: session?.user?.id || "anonymous",
    backendUrl,
    requireAuth,
  });

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    "X-BFF-Secret": trustedSecret,
    ...(session?.user?.id && { "X-User-Id": session.user.id }),
    ...(session?.user?.email && { "X-User-Email": session.user.email }),
    ...((session?.user as any)?.role && { "X-User-Role": (session.user as any).role }),
    ...options.headers,
  };

  const startTime = Date.now();

  try {
    const response = await fetch(url, {
      method: options.method || "GET",
      headers,
      body: options.body ? JSON.stringify(options.body) : undefined,
    });

    const duration = Date.now() - startTime;

    if (!response.ok) {
      const body = await response.json().catch(() => ({}));

      bffLogger.error("Backend API request failed", {
        path,
        status: response.status,
        statusText: response.statusText,
        duration,
        userId: session?.user?.id || "anonymous",
      });

      throw new BackendApiError(response.status, response.statusText, body);
    }

    bffLogger.debug("Backend API request successful", {
      path,
      status: response.status,
      duration,
      userId: session?.user?.id || "anonymous",
    });

    if (response.status === 204) {
      return undefined as T;
    }

    return response.json();
  } catch (error) {
    const duration = Date.now() - startTime;

    if (error instanceof BackendApiError) {
      throw error;
    }

    bffLogger.error("Backend API request exception", {
      path,
      error: error instanceof Error ? error.message : String(error),
      duration,
      userId: session?.user?.id || "anonymous",
    });

    throw error;
  }
}
