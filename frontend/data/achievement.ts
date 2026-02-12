"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";

const achievementLogger = logger.createModuleLogger("achievement-data");

export interface Achievement {
    id: string;
    name?: string;
    description?: string;
    iconUrl?: string;
    points: number;
    category?: string;
    isEarned: boolean;
    earnedAt?: string | null;
}

export interface StreakInfo {
    currentStreak: number;
    longestStreak: number;
    lastLogDate?: string;
}

export interface AchievementListResult {
    achievements: Achievement[];
    totalCount: number;
    totalPages: number;
}

export async function getAchievements(page: number = 1, pageSize: number = 20, sortBy?: string, sortOrder?: string): Promise<AchievementListResult> {
    try {
        const params = new URLSearchParams();
        params.append("Page", page.toString());
        params.append("PageSize", pageSize.toString());
        if (sortBy) params.append("SortBy", sortBy);
        if (sortOrder) params.append("SortOrder", sortOrder);

        const result = await serverApi<{ items: Achievement[], totalCount: number, page: number, pageSize: number, totalPages: number }>(`/api/Achievements?${params}`);
        return {
            achievements: result.items || [],
            totalCount: result.totalCount || 0,
            totalPages: result.totalPages || 0
        };
    } catch (error) {
        achievementLogger.error("Failed to get achievements", { error });
        return { achievements: [], totalCount: 0, totalPages: 0 };
    }
}

export async function getStreak(): Promise<StreakInfo | null> {
    try {
        return await serverApi<StreakInfo>("/api/Achievements/streak");
    } catch (error) {
        achievementLogger.error("Failed to get streak", { error });
        return null;
    }
}

export async function updateStreak(): Promise<boolean> {
    try {
        await serverApi("/api/Achievements/streak", { method: "POST" });
        return true;
    } catch (error) {
        achievementLogger.error("Failed to update streak", { error });
        return false;
    }
}
