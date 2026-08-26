"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";

const adminLogger = logger.createModuleLogger("admin-users-data");

export interface AdminUser {
	id: string;
	email: string;
	name?: string | null;
	image?: string | null;
	role: string;
	emailVerified: boolean;
	banned: boolean;
	banReason?: string | null;
	banExpires?: string | null;
	createdAt: string;
	updatedAt: string;
}

export interface AdminSession {
	id: string;
	userId: string;
	userName?: string | null;
	userEmail?: string | null;
	ipAddress?: string | null;
	userAgent?: string | null;
	createdAt: string;
	lastSeenAt: string;
	expiresAt: string;
}

export interface AdminOverview {
	totalUsers: number;
	activeTrainers: number;
	bannedUsers: number;
	activeSessions: number;
	recentUsers: AdminUser[];
}

export interface AdminUserDetail {
	user: AdminUser;
	activeSessionCount: number;
	recentSessions: AdminSession[];
}

interface Paged<T> {
	items: T[];
	totalCount: number;
	page: number;
	pageSize: number;
	totalPages: number;
}

const EMPTY_OVERVIEW: AdminOverview = {
	totalUsers: 0,
	activeTrainers: 0,
	bannedUsers: 0,
	activeSessions: 0,
	recentUsers: [],
};

export async function getAdminOverview(): Promise<AdminOverview> {
	try {
		return await serverApi<AdminOverview>("/api/admin/overview");
	} catch (error) {
		adminLogger.error("Failed to load admin overview", { error });
		return EMPTY_OVERVIEW;
	}
}

export async function listAdminUsers(params: {
	page?: number;
	pageSize?: number;
	search?: string;
	role?: string;
	banned?: boolean;
	sortBy?: string;
	sortOrder?: string;
}): Promise<Paged<AdminUser>> {
	const query = new URLSearchParams();
	query.set("page", String(params.page ?? 1));
	query.set("pageSize", String(params.pageSize ?? 20));
	if (params.search) query.set("search", params.search);
	if (params.role) query.set("role", params.role);
	if (params.banned !== undefined) query.set("banned", String(params.banned));
	if (params.sortBy) {
		query.set("sortBy", params.sortBy);
		query.set("sortOrder", params.sortOrder ?? "asc");
	}

	try {
		return await serverApi<Paged<AdminUser>>(`/api/admin/users?${query}`);
	} catch (error) {
		adminLogger.error("Failed to list users", { error });
		return { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 };
	}
}

export async function getAdminUser(userId: string): Promise<AdminUserDetail | null> {
	try {
		return await serverApi<AdminUserDetail>(`/api/admin/users/${userId}`);
	} catch (error) {
		adminLogger.error("Failed to load user", { error, userId });
		return null;
	}
}

export async function listAdminSessions(params: {
	page?: number;
	pageSize?: number;
	activeOnly?: boolean;
}): Promise<Paged<AdminSession>> {
	const query = new URLSearchParams();
	query.set("page", String(params.page ?? 1));
	query.set("pageSize", String(params.pageSize ?? 50));
	query.set("activeOnly", String(params.activeOnly ?? true));

	try {
		return await serverApi<Paged<AdminSession>>(`/api/admin/sessions?${query}`);
	} catch (error) {
		adminLogger.error("Failed to list sessions", { error });
		return { items: [], totalCount: 0, page: 1, pageSize: 50, totalPages: 0 };
	}
}
