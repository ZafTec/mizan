"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import type { components } from "@/types/api.generated";

const achievementLogger = logger.createModuleLogger("admin-achievements");

export type AchievementAnalytics = components["schemas"]["GetAchievementAnalyticsResult"];
export type AchievementAnalyticsRow = components["schemas"]["AchievementAnalyticsRow"];
export type AdminAchievement = components["schemas"]["AchievementDto"];

const EMPTY: AchievementAnalytics = {
	totalAchievements: 0,
	totalUsers: 0,
	totalUnlocks: 0,
	usersWithAtLeastOne: 0,
	averageUnlocksPerUser: 0,
	rows: [],
	rowsTotalCount: 0,
	page: 1,
	pageSize: 20,
	totalPages: 0,
	categories: [],
};

/**
 * The catalogue and how it is performing, in one call.
 *
 * Analytics and CRUD used to be two screens. They answer the same question -
 * "is this achievement pulling its weight" - and splitting them meant editing
 * a threshold without seeing that nobody had ever hit the old one.
 */
export async function getAchievementAnalytics(params: {
	page?: number;
	pageSize?: number;
	searchTerm?: string;
	category?: string;
	sortBy?: string;
	sortOrder?: string;
}): Promise<AchievementAnalytics> {
	const query = new URLSearchParams();
	for (const [key, value] of Object.entries(params)) {
		if (value !== undefined && value !== "") query.set(key, String(value));
	}

	try {
		return await serverApi<AchievementAnalytics>(`/api/Achievements/analytics?${query}`);
	} catch (error) {
		achievementLogger.error("Failed to load achievement analytics", { error });
		return EMPTY;
	}
}

export async function getAdminAchievement(id: string): Promise<AdminAchievement | null> {
	try {
		return await serverApi<AdminAchievement>(`/api/Achievements/${id}`);
	} catch (error) {
		achievementLogger.warn("Achievement not found", { error, id });
		return null;
	}
}
