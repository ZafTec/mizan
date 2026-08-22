"use client";

import { resolvePublicApiOrigin } from "@/lib/api-base";
import { safeRedirectPath } from "@/lib/safe-redirect";
import type { User } from "@/lib/auth";

export type { User };
export { useSession, SessionProvider } from "@/components/SessionProvider";

export interface AuthError extends Error {
	status: number;
	code?: string;
}

/**
 * Every auth call is a plain fetch at the API with credentials included; the
 * backend sets and clears the session cookie. There is no client-side auth
 * library since v2 - see docs/REFOCUS.md §6.
 */
async function call<T>(path: string, body?: unknown, method = "POST"): Promise<T> {
	const response = await fetch(`${resolvePublicApiOrigin()}/api/Auth${path}`, {
		method,
		credentials: "include",
		headers: { "Content-Type": "application/json" },
		body: body === undefined ? undefined : JSON.stringify(body),
	});

	if (!response.ok) {
		const payload = await response.json().catch(() => null);
		const error = new Error(
			payload?.error ?? payload?.errors?.[0]?.ErrorMessage ?? "Something went wrong",
		) as AuthError;
		error.status = response.status;
		error.code = payload?.errorCode;
		throw error;
	}

	if (response.status === 204) return undefined as T;
	return (await response.json()) as T;
}

export const signUp = (email: string, password: string, name?: string) =>
	call<{ message: string }>("/register", { email, password, name: name || null });

export const signIn = (email: string, password: string) =>
	call<User>("/login", { email, password });

export const signOut = () => call<void>("/logout");

export const getSession = () => call<User>("/me", undefined, "GET");

export const verifyEmail = (token: string) => call<void>("/verify-email", { token });

export const resendVerification = (email: string) =>
	call<{ message: string }>("/resend-verification", { email });

export const forgotPassword = (email: string) =>
	call<{ message: string }>("/forgot-password", { email });

export const resetPassword = (token: string, password: string) =>
	call<void>("/reset-password", { token, password });

export const changePassword = (currentPassword: string | null, newPassword: string) =>
	call<void>("/change-password", { currentPassword, newPassword });

export const listSessions = () =>
	call<SessionSummary[]>("/sessions", undefined, "GET");

export const revokeSession = (sessionId: string) =>
	call<void>(`/sessions/${sessionId}`, undefined, "DELETE");

export const deleteAccount = () => call<void>("/account", undefined, "DELETE");

/** Full-page navigation: the provider needs to see the browser, not a fetch. */
export function startExternalSignIn(provider: "google" | "github", returnUrl = "/dashboard") {
	const target = new URL(`${resolvePublicApiOrigin()}/api/Auth/external/${provider}`);
	// The backend re-validates this before redirecting; sanitising here too
	// keeps a hostile link from ever reaching it.
	target.searchParams.set("returnUrl", safeRedirectPath(returnUrl));
	window.location.href = target.toString();
}

export interface SessionSummary {
	id: string;
	createdAt: string;
	lastSeenAt: string;
	expiresAt: string;
	ipAddress?: string | null;
	userAgent?: string | null;
	isCurrent: boolean;
}
