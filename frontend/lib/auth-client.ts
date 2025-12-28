"use client";

import { createAuthClient } from "better-auth/react";
import { jwtClient, organizationClient, adminClient } from "better-auth/client/plugins";
import { ac, adminRole, trainerRole, userRole } from "@/lib/permissions";

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
    const { data, error } = await authClient.token();

    if (error) {
      console.error("Failed to get API token:", error);
      await handleSessionExpired();
      return null;
    }

    return data?.token || null;
  } catch (error) {
    console.error("Failed to get API token:", error);
    await handleSessionExpired();
    return null;
  }
}

// Handle expired or invalid session
async function handleSessionExpired(): Promise<void> {
  if (typeof window === 'undefined') return;

  try {
    await authClient.signOut({ fetchOptions: { onSuccess: () => { } } });
  } catch (error) {
    console.error("Error signing out:", error);
  }

  const currentPath = window.location.pathname;
  if (currentPath !== '/login' && currentPath !== '/signup') {
    window.location.href = `/login?error=session_expired&redirect=${encodeURIComponent(currentPath)}`;
  }
}

// API client with automatic token injection and case conversion
export async function apiClient<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const baseUrl = (typeof window === 'undefined' ? process.env.API_URL : (typeof process !== 'undefined' && process.env["NEXT_PUBLIC_API_URL"])) || "http://localhost:5000";
  const apiUrl = baseUrl;

  let headers: HeadersInit = {
    "Content-Type": "application/json",
    ...options.headers,
  };

  // Get token from BetterAuth
  const token = await getApiToken();
  if (token) {
    headers = {
      ...headers,
      Authorization: `Bearer ${token}`,
    };
  }

  const response = await fetch(`${apiUrl}${endpoint}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    if (response.status === 401) {
      console.error(`[apiClient] Unauthorized: ${endpoint} - Token may be expired`);
      await handleSessionExpired();
      throw new Error("Session expired. Please log in again.");
    }

    const error = await response.text();
    console.error(`[apiClient] Request failed: ${endpoint} - Status: ${response.status}`);
    throw new Error(error || `API error: ${response.status}`);
  }

  const data = await response.json();

  // Convert PascalCase keys from backend to camelCase for frontend
  // This ensures compatibility between C# DTOs and TypeScript types
  return convertKeysToCamelCase<T>(data);
}

// Validate current session and handle expiry
export async function validateSession(): Promise<boolean> {
  try {
    const { data, error } = await authClient.getSession();

    if (error || !data?.session) {
      await handleSessionExpired();
      return false;
    }

    return true;
  } catch (error) {
    console.error("Session validation error:", error);
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
