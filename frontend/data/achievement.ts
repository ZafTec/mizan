"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import type { AchievementDto, AchievementListResultDto, StreakDto } from "@/types/api-contracts";

const achievementLogger = logger.createModuleLogger("achievement-data");

export type Achievement = AchievementDto;
export type StreakInfo = StreakDto;
export type AchievementListResult = AchievementListResultDto;

export async function getAchievements(page: number = 1, pageSize: number = 50, sortBy?: string, sortOrder?: string) {
    try {
        const params = new URLSearchParams();
        params.append("Page", page.toString());
        params.append("PageSize", pageSize.toString());
        if (sortBy) params.append("SortBy", sortBy);
        if (sortOrder) params.append("SortOrder", sortOrder);

        const result = await serverApi<AchievementListResultDto>(`/api/Achievements?${params}`);
        return {
            achievements: result.items ?? [],
            totalCount: result.totalCount ?? 0,
            totalPages: result.totalPages ?? 0,
            totalPoints: (result as AchievementListResultDto & { totalPoints?: number }).totalPoints ?? 0,
            earnedCount: (result as AchievementListResultDto & { earnedCount?: number }).earnedCount ?? 0,
            level: (result as AchievementListResultDto & { level?: number }).level ?? 1,
            levelName: (result as AchievementListResultDto & { levelName?: string }).levelName ?? "Rookie",
            levelFloor: (result as AchievementListResultDto & { levelFloor?: number }).levelFloor ?? 0,
            nextLevelAt: (result as AchievementListResultDto & { nextLevelAt?: number | null }).nextLevelAt ?? null,
        };
    } catch (error) {
        achievementLogger.error("Failed to get achievements", { error });
        return {
            achievements: [],
            totalCount: 0,
            totalPages: 0,
            totalPoints: 0,
            earnedCount: 0,
            level: 1,
            levelName: "Rookie",
            levelFloor: 0,
            nextLevelAt: null as number | null,
        };
    }
}

export async function getStreak(streakType: string = "nutrition"): Promise<StreakInfo | null> {
    try {
        const params = new URLSearchParams({ streakType });
        return await serverApi<StreakInfo>(`/api/Achievements/streak?${params}`);
    } catch (error) {
        achievementLogger.error("Failed to get streak", { error, streakType });
        return null;
    }
}
