import { ApiError, convertKeysToCamelCase } from "@/lib/api";
import { getApiToken } from "@/lib/api.client";
import { resolvePublicApiOrigin } from "@/lib/api-base";
import { clientApi } from "@/lib/api.client";
import type { McpUsageAnalyticsResult } from "@/types/mcp";

export interface UserOverviewResponse {
	id: string;
	email: string;
	name: string | null;
	image: string | null;
	createdAt: string;
	streakCount: number;
	currentGoal: {
		targetCalories: number | null;
		targetProteinGrams: number | null;
		targetCarbsGrams: number | null;
		targetFatGrams: number | null;
	} | null;
}

export interface StreakResponse {
	currentStreak: number;
	longestStreak: number;
	lastActivityDate: string | null;
	isActiveToday: boolean;
}

export interface MealsRangeResponse {
	days: Array<{
		date: string;
		calories: number;
		protein: number;
		carbs: number;
		fat: number;
		fiber: number;
	}>;
}

export interface AchievementsResponse {
	earnedCount: number;
	totalPoints: number;
}

export interface ProfileObservations {
	joinedAt: string;
	streakCount: number;
	longestStreak: number;
	goalSummary: string;
	mealLoggingDays: number;
	averageCalories: number;
	achievementCount: number;
	totalAchievementPoints: number;
	mcpCalls: number;
	mcpSuccessRate: number;
}

function buildGoalSummary(user: UserOverviewResponse): string {
	if (!user.currentGoal) {
		return "No active goal set";
	}

	const parts = [
		user.currentGoal.targetCalories ? `${Math.round(user.currentGoal.targetCalories)} kcal` : null,
		user.currentGoal.targetProteinGrams ? `${Math.round(user.currentGoal.targetProteinGrams)}g protein` : null,
		user.currentGoal.targetCarbsGrams ? `${Math.round(user.currentGoal.targetCarbsGrams)}g carbs` : null,
		user.currentGoal.targetFatGrams ? `${Math.round(user.currentGoal.targetFatGrams)}g fat` : null,
	].filter(Boolean);

	return parts.length > 0 ? parts.join(" - ") : "Active goal without macro targets";
}

export async function getProfileObservations(): Promise<ProfileObservations> {
	const [user, streak, mealsRange, achievements, mcpAnalytics] = await Promise.all([
		clientApi<UserOverviewResponse>("/api/Users/me"),
		clientApi<StreakResponse>("/api/Achievements/streak").catch(() => ({
			currentStreak: 0,
			longestStreak: 0,
			lastActivityDate: null,
			isActiveToday: false,
		})),
		clientApi<MealsRangeResponse>("/api/Meals/range?days=14").catch(() => ({ days: [] })),
		clientApi<AchievementsResponse>("/api/Achievements?page=1&pageSize=1").catch(() => ({
			earnedCount: 0,
			totalPoints: 0,
		})),
		clientApi<McpUsageAnalyticsResult>("/api/McpTokens/analytics").catch(() => ({
			overview: {
				totalCalls: 0,
				successfulCalls: 0,
				failedCalls: 0,
				successRate: 0,
				averageExecutionTimeMs: 0,
				uniqueTokensUsed: 0,
			},
			toolUsage: [],
			tokenUsage: [],
			dailyUsage: [],
		})),
	]);

	const loggedDays = mealsRange.days.filter((day) => day.calories > 0);
	const totalCalories = loggedDays.reduce((sum, day) => sum + day.calories, 0);

	return {
		joinedAt: user.createdAt,
		streakCount: streak.currentStreak || user.streakCount || 0,
		longestStreak: streak.longestStreak || 0,
		goalSummary: buildGoalSummary(user),
		mealLoggingDays: loggedDays.length,
		averageCalories: loggedDays.length > 0 ? Math.round(totalCalories / loggedDays.length) : 0,
		achievementCount: achievements.earnedCount,
		totalAchievementPoints: achievements.totalPoints,
		mcpCalls: mcpAnalytics.overview?.totalCalls ?? 0,
		mcpSuccessRate: Math.round(mcpAnalytics.overview?.successRate ?? 0),
	};
}

export async function downloadProfileExport(): Promise<{ blob: Blob; filename: string }> {
	const token = await getApiToken();
	if (!token) {
		throw new ApiError(401, "Unauthorized", { error: "Missing token" });
	}

	const response = await fetch(`${resolvePublicApiOrigin()}/api/Profile/export`, {
		headers: {
			Authorization: `Bearer ${token}`,
		},
	});

	if (!response.ok) {
		const body = convertKeysToCamelCase(await response.json().catch(() => ({})));
		throw new ApiError(response.status, response.statusText, body);
	}

	const disposition = response.headers.get("content-disposition") ?? "";
	const match = disposition.match(/filename="?([^\"]+)"?/i);
	return {
		blob: await response.blob(),
		filename: match?.[1] ?? `mizan-profile-export-${new Date().toISOString().slice(0, 10)}.json`,
	};
}
