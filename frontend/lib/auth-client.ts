"use client";

import { createAuthClient } from "better-auth/react";
import { jwtClient, organizationClient, adminClient } from "better-auth/client/plugins";
import { ac, adminRole, trainerRole, userRole } from "@/lib/permissions";
import { logger } from "@/lib/logger";

const authLogger = logger.createModuleLogger("auth-client");

export const authClient = createAuthClient({
  baseURL: (typeof process !== 'undefined' && process.env.NEXT_PUBLIC_APP_URL) ? process.env.NEXT_PUBLIC_APP_URL : (() => { throw new Error("BETTER_AUTH_URL is not defined"); })(),
  plugins: [
    jwtClient(),
    organizationClient(),
    adminClient({
      ac,
      roles: {
        user: userRole,
        trainer: trainerRole,
        admin: adminRole,
      },
    }),
  ],
});

// Export typed hooks and utilities
export const {
  signIn,
  signUp,
  signOut,
  useSession,
  getSession,
  organization,
} = authClient;

// Get JWT token for API calls
export async function getApiToken(): Promise<string | null> {
  try {
    authLogger.debug("Requesting API token");

    const { data, error } = await authClient.token();

    if (error) {
      authLogger.error("Failed to get API token", { error: error.message || error });
      await handleSessionExpired();
      return null;
    }

    authLogger.debug("API token retrieved successfully");
    return data?.token || null;
  } catch (error) {
    authLogger.error("Exception getting API token", {
      error: error instanceof Error ? error.message : String(error),
    });
    await handleSessionExpired();
    return null;
  }
}

// Handle expired or invalid session
async function handleSessionExpired(): Promise<void> {
  if (typeof window === 'undefined') return;

  const currentPath = window.location.pathname;
  authLogger.warn("Session expired", { currentPath });

  try {
    await authClient.signOut({ fetchOptions: { onSuccess: () => { } } });
    authLogger.info("Sign out successful");
  } catch (error) {
    authLogger.error("Error signing out", {
      error: error instanceof Error ? error.message : String(error),
    });
  }

  if (currentPath !== '/login' && currentPath !== '/signup') {
    const redirectUrl = `/login?error=session_expired&redirect=${encodeURIComponent(currentPath)}`;
    authLogger.info("Redirecting to login", { from: currentPath, to: redirectUrl });
    window.location.href = redirectUrl;
  }
}

// API client with automatic token injection and case conversion
export async function apiClient<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const baseUrl = (typeof window === 'undefined' ? process.env.API_URL : (typeof process !== 'undefined' && process.env["NEXT_PUBLIC_API_URL"])) || "http://localhost:5000";
  const apiUrl = baseUrl;

  authLogger.debug("API request starting", {
    endpoint,
    method: options.method || "GET",
    apiUrl,
    isServer: typeof window === 'undefined',
  });

  let headers: HeadersInit = {
    "Content-Type": "application/json",
    ...options.headers,
  };

  const token = await getApiToken();
  if (token) {
    headers = {
      ...headers,
      Authorization: `Bearer ${token}`,
    };
    authLogger.debug("Token attached to request");
  } else {
    authLogger.warn("No token available for request", { endpoint });
  }

  const startTime = Date.now();

  try {
    const response = await fetch(`${apiUrl}${endpoint}`, {
      ...options,
      headers,
    });

    const duration = Date.now() - startTime;

    if (!response.ok) {
      if (response.status === 401) {
        authLogger.error("Unauthorized request", {
          endpoint,
          status: response.status,
          duration,
        });
        await handleSessionExpired();
        throw new Error("Session expired. Please log in again.");
      }

      const error = await response.text();
      authLogger.error("API request failed", {
        endpoint,
        status: response.status,
        statusText: response.statusText,
        error,
        duration,
      });
      throw new Error(error || `API error: ${response.status}`);
    }

    const data = await response.json();
    authLogger.debug("API request successful", {
      endpoint,
      status: response.status,
      duration,
    });

    return convertKeysToCamelCase<T>(data);
  } catch (error) {
    const duration = Date.now() - startTime;
    authLogger.error("API request exception", {
      endpoint,
      error: error instanceof Error ? error.message : String(error),
      duration,
    });
    throw error;
  }
}

// Validate current session and handle expiry
export async function validateSession(): Promise<boolean> {
  try {
    authLogger.debug("Validating session");

    const { data, error } = await authClient.getSession();

    if (error || !data?.session) {
      authLogger.warn("Session validation failed", {
        hasError: !!error,
        hasSession: !!data?.session,
      });
      await handleSessionExpired();
      return false;
    }

    authLogger.debug("Session validated successfully", {
      userId: data.session.userId,
    });
    return true;
  } catch (error) {
    authLogger.error("Session validation exception", {
      error: error instanceof Error ? error.message : String(error),
    });
    await handleSessionExpired();
    return false;
  }
}

// Helper function to convert PascalCase to camelCase recursively
function convertKeysToCamelCase<T>(obj: any): T {
  if (obj === null || obj === undefined) {
    return obj;
  }

  if (Array.isArray(obj)) {
    return obj.map(item => convertKeysToCamelCase(item)) as T;
  }

  if (typeof obj === 'object' && obj.constructor === Object) {
    const converted: any = {};
    for (const key in obj) {
      if (Object.prototype.hasOwnProperty.call(obj, key)) {
        const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
        converted[camelKey] = convertKeysToCamelCase(obj[key]);
      }
    }
    return converted as T;
  }

  return obj;
}
