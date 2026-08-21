"use server";

import { serverApi } from "@/lib/api.server";
import { ApiError } from "@/lib/api";
import { logger } from "@/lib/logger";

const trainerLogger = logger.createModuleLogger("trainer-data");

export interface MyTrainer {
	relationshipId: string;
	trainerId: string;
	trainerName?: string | null;
	trainerEmail?: string | null;
	trainerImage?: string | null;
	status: string;
	canViewNutrition: boolean;
	canViewWorkouts: boolean;
	canViewMeasurements: boolean;
	canMessage: boolean;
	startedAt: string;
	endedAt?: string | null;
}

/**
 * Null means "no coaching relationship", which is the common case and not an
 * error - the API answers 404 for it. Any other failure is logged and also
 * degrades to null so a contextual surface can never break the page it is on.
 */
export async function getMyTrainer(): Promise<MyTrainer | null> {
	try {
		return await serverApi<MyTrainer>("/api/Trainers/my-trainer");
	} catch (error) {
		if (error instanceof ApiError && error.status === 404) return null;
		trainerLogger.error("Failed to load trainer relationship", { error });
		return null;
	}
}
