"use client";

import { toast } from "sonner";
import { ApiError } from "@/lib/api";

function readBodyMessage(body: unknown): string | null {
	if (!body) {
		return null;
	}

	if (typeof body === "string") {
		return body;
	}

	if (typeof body !== "object") {
		return null;
	}

	const record = body as Record<string, unknown>;
	const direct = [record.message, record.error, record.title, record.raw].find(
		(value) => typeof value === "string" && value.trim().length > 0
	);

	if (typeof direct === "string") {
		return direct;
	}

	if (record.error && typeof record.error === "object") {
		return readBodyMessage(record.error);
	}

	return null;
}

export function getErrorMessage(error: unknown, fallback = "Something went wrong"): string {
	if (typeof error === "string" && error.trim()) {
		return error;
	}

	if (error instanceof ApiError) {
		return readBodyMessage(error.body) ?? fallback;
	}

	if (error instanceof Error && error.message.trim()) {
		return error.message;
	}

	return fallback;
}

export const appToast = {
	success(message: string, description?: string) {
		toast.success(message, { description });
	},
	info(message: string, description?: string) {
		toast.info(message, { description });
	},
	error(error: unknown, fallback = "Something went wrong") {
		toast.error(getErrorMessage(error, fallback));
	},
	message(message: string, description?: string) {
		toast(message, { description });
	},
};
