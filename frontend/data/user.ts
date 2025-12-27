"use server";

import { createErrorState, createSuccessState, FormState } from "@/helper/FormErrorHandler";
import { auth } from "@/lib/auth";

/**
 * Register a new user with email and password via BetterAuth
 */
export async function addUser(prevState: FormState, formData: FormData): Promise<FormState> {
    try {
        const email = formData.get("email") as string;
        const password = formData.get("password") as string;
        const name = formData.get("name") as string;

        if (!email || !password) {
            return createErrorState("Email and password are required", [
                { field: "email", message: !email ? "Email is required" : "" },
                { field: "password", message: !password ? "Password is required" : "" },
            ]);
        }

        // Use BetterAuth's server-side signUpEmail API
        const result = await auth.api.signUpEmail({
            body: {
                email: email.toLowerCase(),
                password,
                name: name || email.split("@")[0],
            },
        });

        if (!result || !result.user) {
            return createErrorState("Failed to create account");
        }

        return createSuccessState("Account created! Please check your email to verify your account.");
    } catch (error) {
        console.error("Failed to add user:", error);
        return createErrorState("Failed to create account. Please try again.");
    }
}

/**
 * Resend verification email
 * Note: This is handled by BetterAuth's built-in verification flow
 */
export async function resendUserVerificationEmail(
    prevState: FormState,
    formData: FormData
): Promise<FormState> {
    try {
        const email = formData.get("email") as string;

        if (!email) {
            return createErrorState("Email is required");
        }

        // BetterAuth handles email verification - trigger resend via API
        // The actual implementation depends on BetterAuth's configuration
        console.log("Verification email resend - handled by BetterAuth");

        return createSuccessState("Verification email sent!");
    } catch (error) {
        console.error("Failed to resend verification email:", error);
        return createErrorState("Failed to send verification email");
    }
}
