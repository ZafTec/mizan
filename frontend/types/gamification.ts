// Gamification types re-exported from OpenAPI-generated schemas so UI code imports a
// stable path even if codegen paths shift later. Safe to treat these as the source
// of truth for shape; runtime backend already guarantees camelCase keys.

import type {
    StreakUpdateDto,
    UnlockedAchievementDto,
} from "@/types/api-contracts";

export type StreakUpdate = StreakUpdateDto;
export type UnlockedAchievement = UnlockedAchievementDto;

export interface GamificationFeedback {
    streak?: StreakUpdate | null;
    unlockedAchievements?: UnlockedAchievement[] | null;
}

export interface LevelInfo {
    level: number;
    levelName: string;
    levelFloor: number;
    nextLevelAt: number | null;
    totalPoints: number;
}
