"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import { createErrorState, createSuccessState, FormState, FieldError } from "@/helper/FormErrorHandler";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

const adminAchievementLogger = logger.createModuleLogger("admin-achievement");

export interface AdminAchievement {
    id: string;
    name: string;
    description?: string | null;
    iconUrl?: string | null;
    points: number;
    category?: string | null;
    criteriaType?: string | null;
    threshold: number;
    isEarned: boolean;
    earnedAt?: string | null;
}

export interface AdminAchievementList {
    items: AdminAchievement[];
    totalCount: number;
    totalPages: number;
}

export interface AchievementListQuery {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    category?: string;
    sortBy?: string;
    sortOrder?: "asc" | "desc";
}

export async function getAchievementsAdmin(query: AchievementListQuery = {}): Promise<AdminAchievementList> {
    try {
        const params = new URLSearchParams({
            Page: String(query.page ?? 1),
            PageSize: String(query.pageSize ?? 20),
        });
        if (query.searchTerm) params.set("SearchTerm", query.searchTerm);
        if (query.category) params.set("Category", query.category);
        if (query.sortBy) params.set("SortBy", query.sortBy);
        if (query.sortOrder) params.set("SortOrder", query.sortOrder);

        const result = await serverApi<{
            items: AdminAchievement[];
            totalCount: number;
            totalPages: number;
        }>(`/api/Achievements?${params}`);
        return {
            items: result.items ?? [],
            totalCount: result.totalCount ?? 0,
            totalPages: result.totalPages ?? 0,
        };
    } catch (error) {
        adminAchievementLogger.error("Failed to list achievements", { error });
        return { items: [], totalCount: 0, totalPages: 0 };
    }
}

export async function getAchievementById(id: string): Promise<AdminAchievement | null> {
    try {
        return await serverApi<AdminAchievement>(`/api/Achievements/${id}`);
    } catch (error) {
        adminAchievementLogger.error("Failed to load achievement", { error, id });
        return null;
    }
}

function parseAchievementForm(formData: FormData): { body: Record<string, unknown>; fieldErrors: FieldError[] } {
    const name = (formData.get("name") as string | null)?.trim() ?? "";
    const description = (formData.get("description") as string | null)?.trim() || null;
    const iconUrl = (formData.get("iconUrl") as string | null)?.trim() || null;
    const pointsRaw = formData.get("points") as string | null;
    const thresholdRaw = formData.get("threshold") as string | null;
    const category = (formData.get("category") as string | null)?.trim() || null;
    const criteriaType = (formData.get("criteriaType") as string | null)?.trim() || null;

    const fieldErrors: FieldError[] = [];
    if (!name) fieldErrors.push({ field: "name", message: "Name is required" });
    if (name.length > 100) fieldErrors.push({ field: "name", message: "Name must be 100 characters or fewer" });

    const points = Number.parseInt(pointsRaw ?? "0", 10);
    if (Number.isNaN(points) || points < 0 || points > 10_000) {
        fieldErrors.push({ field: "points", message: "Points must be between 0 and 10,000" });
    }

    const threshold = Number.parseInt(thresholdRaw ?? "0", 10);
    if (Number.isNaN(threshold) || threshold < 0) {
        fieldErrors.push({ field: "threshold", message: "Threshold must be zero or greater" });
    }

    return {
        body: {
            name,
            description,
            iconUrl,
            points: Number.isNaN(points) ? 0 : points,
            threshold: Number.isNaN(threshold) ? 0 : threshold,
            category,
            criteriaType,
        },
        fieldErrors,
    };
}

export async function createAchievementAction(_: FormState, formData: FormData): Promise<FormState> {
    const { body, fieldErrors } = parseAchievementForm(formData);
    if (fieldErrors.length > 0) {
        return { status: "error", message: "Check the highlighted fields", fieldErrors };
    }
    try {
        await serverApi<{ id: string }>("/api/Achievements", { method: "POST", body });
    } catch (error) {
        adminAchievementLogger.error("Create achievement failed", { error });
        return createErrorState("Could not create achievement");
    }
    revalidatePath("/admin/achievements");
    redirect("/admin/achievements");
}

export async function updateAchievementAction(id: string, _: FormState, formData: FormData): Promise<FormState> {
    const { body, fieldErrors } = parseAchievementForm(formData);
    if (fieldErrors.length > 0) {
        return { status: "error", message: "Check the highlighted fields", fieldErrors };
    }
    try {
        await serverApi(`/api/Achievements/${id}`, {
            method: "PUT",
            body: { id, ...body },
        });
    } catch (error) {
        adminAchievementLogger.error("Update achievement failed", { error, id });
        return createErrorState("Could not update achievement");
    }
    revalidatePath("/admin/achievements");
    revalidatePath(`/admin/achievements/${id}/edit`);
    return createSuccessState("Achievement saved");
}

export async function deleteAchievementAction(id: string): Promise<{ success: boolean; message?: string }> {
    try {
        await serverApi(`/api/Achievements/${id}`, { method: "DELETE" });
        revalidatePath("/admin/achievements");
        return { success: true };
    } catch (error) {
        adminAchievementLogger.error("Delete achievement failed", { error, id });
        return { success: false, message: "Delete failed" };
    }
}

export interface AchievementAnalyticsRow {
    id: string;
    name: string;
    category?: string | null;
    criteriaType?: string | null;
    threshold: number;
    points: number;
    unlockedBy: number;
    unlockRate: number;
    mostRecentUnlockAt?: string | null;
}

export interface AchievementAnalyticsCategory {
    category: string;
    achievementCount: number;
    totalUnlocks: number;
    totalPointsEarned: number;
}

export interface AchievementAnalytics {
    totalAchievements: number;
    totalUsers: number;
    totalUnlocks: number;
    usersWithAtLeastOne: number;
    averageUnlocksPerUser: number;
    rows: AchievementAnalyticsRow[];
    rowsTotalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
    categories: AchievementAnalyticsCategory[];
}

export interface AnalyticsQuery {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    category?: string;
    sortBy?: string;
    sortOrder?: "asc" | "desc";
}

export async function getAchievementAnalytics(query: AnalyticsQuery = {}): Promise<AchievementAnalytics | null> {
    try {
        const params = new URLSearchParams({
            Page: String(query.page ?? 1),
            PageSize: String(query.pageSize ?? 20),
        });
        if (query.searchTerm) params.set("SearchTerm", query.searchTerm);
        if (query.category) params.set("Category", query.category);
        if (query.sortBy) params.set("SortBy", query.sortBy);
        if (query.sortOrder) params.set("SortOrder", query.sortOrder);

        return await serverApi<AchievementAnalytics>(`/api/Achievements/analytics?${params}`);
    } catch (error) {
        adminAchievementLogger.error("Failed to load analytics", { error });
        return null;
    }
}
