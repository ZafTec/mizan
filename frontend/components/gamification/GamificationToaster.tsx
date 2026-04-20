"use client";

import { useEffect, useRef } from "react";
import { toast } from "sonner";
import type { StreakUpdate, UnlockedAchievement } from "@/types/gamification";

interface GamificationToasterProps {
    streak?: StreakUpdate | null;
    unlockedAchievements?: UnlockedAchievement[] | null;
}

/**
 * Fires toasts when a form action returns gamification feedback. Dedupes per
 * streak-day and per achievement id so re-renders of the same FormState do not
 * re-toast.
 */
export function GamificationToaster({ streak, unlockedAchievements }: GamificationToasterProps) {
    const firedStreakKey = useRef<string | null>(null);
    const firedAchievementIds = useRef<Set<string>>(new Set());

    useEffect(() => {
        if (!streak || !streak.extended) return;
        const key = `${streak.streakType}:${streak.lastActivityDate}:${streak.currentCount}`;
        if (firedStreakKey.current === key) return;
        firedStreakKey.current = key;

        if (streak.isNewRecord) {
            toast.success(`New record: ${streak.currentCount}-day ${streak.streakType} streak!`, {
                description: `Your longest streak ever. Keep it rolling.`,
                duration: 6000,
            });
        } else if (streak.currentCount === 1) {
            toast.success(`${streak.streakType.charAt(0).toUpperCase()}${streak.streakType.slice(1)} streak started`, {
                description: `Log again tomorrow to keep it going.`,
                duration: 4000,
            });
        } else {
            toast.success(`${streak.currentCount}-day ${streak.streakType} streak`, {
                description: `Longest: ${streak.longestCount} days.`,
                duration: 4000,
            });
        }
    }, [streak]);

    useEffect(() => {
        if (!unlockedAchievements || unlockedAchievements.length === 0) return;
        for (const a of unlockedAchievements) {
            if (firedAchievementIds.current.has(a.id)) continue;
            firedAchievementIds.current.add(a.id);
            toast.success(`Achievement unlocked: ${a.name}`, {
                description: `${a.description ?? ""} ${a.points > 0 ? `(+${a.points} pts)` : ""}`.trim(),
                duration: 7000,
            });
        }
    }, [unlockedAchievements]);

    return null;
}
