"use server";

import { createErrorState, createSuccessState, FormState } from "@/helper/FormErrorHandler";
import { ApiError, request } from "@/lib/api";
import { serverApiOrigin } from "@/lib/auth";
import { logger } from "@/lib/logger";

const userLogger = logger.createModuleLogger("user-data");

const MIN_PASSWORD_LENGTH = 10;

/**
 * Registration and verification are backend endpoints since v2; these actions
 * only validate the form and relay - see docs/REFOCUS.md §6.
 */
async function postAnonymous(path: string, body: unknown): Promise<void> {
	await request(serverApiOrigin(), path, { mode: "none" }, {
		method: "POST",
		body,
		requireAuth: false,
	});
}

function apiMessage(error: unknown): string | null {
	if (error instanceof ApiError && error.body && typeof error.body === "object") {
		const message = (error.body as { error?: unknown }).error;
		if (typeof message === "string" && message.trim()) return message;
	}
	return null;
}

export async function addUser(prevState: FormState, formData: FormData): Promise<FormState> {
	const email = formData.get("email") as string;
	const password = formData.get("password") as string;
	const confirmPassword = formData.get("confirmPassword") as string;
	const name = formData.get("name") as string;

	if (!email || !password) {
		return createErrorState("Email and password are required", [
			{ field: "email", message: !email ? "Email is required" : "" },
			{ field: "password", message: !password ? "Password is required" : "" },
		]);
	}

	if (password !== confirmPassword) {
		return createErrorState("Passwords do not match", [
			{ field: "confirmPassword", message: "Passwords do not match" },
		]);
	}

	if (password.length < MIN_PASSWORD_LENGTH) {
		const message = `Password must be at least ${MIN_PASSWORD_LENGTH} characters`;
		return createErrorState(message, [{ field: "password", message }]);
	}

	try {
		await postAnonymous("/api/Auth/register", {
			email: email.toLowerCase(),
			password,
			name: name || email.split("@")[0],
		});
	} catch (error) {
		const message = apiMessage(error);
		userLogger.error("Failed to create user account", { error });

		if (message?.includes("already exists")) {
			return createErrorState(message, [
				{ field: "email", message: "This email is already registered" },
			]);
		}

		return createErrorState(message ?? "Failed to create account. Please try again.");
	}

	return createSuccessState("Account created. Check your email to confirm your address.");
}

export async function resendUserVerificationEmail(
	prevState: FormState,
	formData: FormData,
): Promise<FormState> {
	const email = formData.get("email") as string;

	if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
		return createErrorState("Invalid email format", [
			{ field: "email", message: "Please enter a valid email address" },
		]);
	}

	try {
		await postAnonymous("/api/Auth/resend-verification", { email: email.toLowerCase() });
	} catch (error) {
		// The endpoint says nothing about whether the address exists, and neither
		// does this: a failure here is logged, not surfaced as enumeration.
		userLogger.error("Failed to resend verification email", { error });
	}

	return createSuccessState(
		"If an account exists with this email, a verification link has been sent. Check your inbox and spam folder.",
	);
}
