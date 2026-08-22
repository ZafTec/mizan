import { logger } from "@/lib/logger";

const apiLogger = logger.createModuleLogger("api");

export class ApiError extends Error {
  constructor(
    public status: number,
    public statusText: string,
    public body: unknown,
  ) {
    super(`API error: ${status} ${statusText}`);
  }
}

export function convertKeysToCamelCase<T>(obj: unknown): T {
  if (obj === null || obj === undefined) return obj as T;

  if (Array.isArray(obj)) {
    return obj.map((item) => convertKeysToCamelCase(item)) as T;
  }

  if (typeof obj === "object" && obj.constructor === Object) {
    const converted: Record<string, unknown> = {};
    for (const key in obj as Record<string, unknown>) {
      if (Object.prototype.hasOwnProperty.call(obj, key)) {
        const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
        converted[camelKey] = convertKeysToCamelCase(
          (obj as Record<string, unknown>)[key],
        );
      }
    }
    return converted as T;
  }

  return obj as T;
}

function safeJsonParse(text: string): unknown | undefined {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

export interface ApiRequestOptions {
  method?: string;
  body?: unknown;
  headers?: Record<string, string>;
  requireAuth?: boolean;
  expectedStatuses?: number[];
}

/**
 * How this call proves who is making it. Since v2 that is always the session
 * cookie: the browser attaches it itself, and the Next server forwards the one
 * it received. There is no bearer token any more - see docs/REFOCUS.md §6.
 */
export type RequestAuth =
  | { mode: "none" }
  | { mode: "browser" }
  | { mode: "cookie"; cookie: string };

export async function request<T>(
  baseUrl: string,
  path: string,
  auth: RequestAuth,
  options: ApiRequestOptions = {},
): Promise<T> {
  const {
    method = "GET",
    body,
    headers: extraHeaders,
    requireAuth = true,
    expectedStatuses = [],
  } = options;

  if (requireAuth && auth.mode === "none") {
    throw new ApiError(401, "Unauthorized", { error: "Not authenticated" });
  }

  const url = `${baseUrl}${path}`;
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    // Harmless outside ngrok; bypasses ngrok's free-tier browser-warning
    // interstitial, which otherwise intercepts API calls made through a tunnel.
    "ngrok-skip-browser-warning": "true",
    ...(auth.mode === "cookie" ? { Cookie: auth.cookie } : {}),
    ...extraHeaders,
  };

  const startTime = Date.now();

  try {
    const response = await fetch(url, {
      method,
      headers,
      // The browser holds the cookie; on the server it rides in the header above.
      credentials: auth.mode === "browser" ? "include" : "omit",
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });

    const duration = Date.now() - startTime;

    if (!response.ok) {
      const rawBody = await response.text().catch(() => "");
      const parsed = rawBody
        ? (safeJsonParse(rawBody) ?? { raw: rawBody })
        : {};

      const details = { path, status: response.status, duration };
      if (expectedStatuses.includes(response.status))
        apiLogger.debug("Request returned expected status", details);
      else apiLogger.error("Request failed", details);

      throw new ApiError(response.status, response.statusText, parsed);
    }

    apiLogger.debug("Request successful", {
      path,
      status: response.status,
      duration,
    });

    if (response.status === 204) return undefined as T;

    const raw = await response.text();
    const data = safeJsonParse(raw) ?? (raw as unknown);
    return convertKeysToCamelCase<T>(data);
  } catch (error) {
    if (error instanceof ApiError) throw error;

    const duration = Date.now() - startTime;
    apiLogger.error("Request exception", {
      path,
      error: error instanceof Error ? error.message : String(error),
      duration,
    });
    throw error;
  }
}
