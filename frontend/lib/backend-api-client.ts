import "server-only";
import { auth } from "@/lib/auth";

interface BackendApiOptions {
  method?: string;
  body?: any;
  headers?: Record<string, string>;
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
  // Get authenticated session (server-side only)
  const session = await auth.api.getSession({ headers: await import("next/headers").then(m => m.headers()) });

  if (!session?.user?.id) {
    throw new BackendApiError(401, "Unauthorized", { error: "Not authenticated" });
  }

  const backendUrl = process.env.BACKEND_API_URL || "http://backend:8080";
  const trustedSecret = process.env.BFF_TRUSTED_SECRET;

  if (!trustedSecret) {
    throw new Error("BFF_TRUSTED_SECRET not configured");
  }

  const url = `${backendUrl}${path}`;

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    "X-BFF-Secret": trustedSecret,
    "X-User-Id": session.user.id,
    ...options.headers,
  };

  // Optional: Pass email and role if available
  if (session.user.email) {
    headers["X-User-Email"] = session.user.email;
  }

  if ((session.user as any).role) {
    headers["X-User-Role"] = (session.user as any).role;
  }

  const response = await fetch(url, {
    method: options.method || "GET",
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new BackendApiError(response.status, response.statusText, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
}
