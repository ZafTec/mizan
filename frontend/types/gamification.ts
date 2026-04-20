// Gamification surface: streak updates, achievement unlocks, and user level.
// Mirrors backend StreakUpdate / UnlockedAchievement / GetAchievementsResult so the
// UI can react immediately after a command completes without another round trip.
//
// These live here (and not in api.generated.ts) because they're shared across
// several response DTOs (meal log, workout log, body measurement); if the OpenAPI
// codegen ever splits them out, this file can re-export from the generated set.

export interface StreakUpdate {
    streakType: string;
    currentCount: number;
    longestCount: number;
    isNewRecord: boolean;
    extended: boolean;
    lastActivityDate: string;
}

export interface UnlockedAchievement {
    id: string;
    name: string;
    description?: string | null;
    iconUrl?: string | null;
    points: number;
    category?: string | null;
}

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
