"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";

const relLogger = logger.createModuleLogger("admin-relationships");

export interface AdminRelationship {
	id: string;
	trainerId: string;
	trainerName?: string | null;
	trainerEmail: string;
	clientId: string;
	clientName?: string | null;
	clientEmail: string;
	status: string;
	canViewNutrition: boolean;
	canViewWorkouts: boolean;
	canViewMeasurements: boolean;
	canMessage: boolean;
	startedAt?: string | null;
	endedAt?: string | null;
	createdAt: string;
}

export interface AdminRelationshipPage {
	items: AdminRelationship[];
	totalCount: number;
	page: number;
	pageSize: number;
	totalPages: number;
}

export async function listAdminRelationships(params: {
	page?: number;
	pageSize?: number;
	search?: string;
	status?: string;
	sortBy?: string;
	sortOrder?: string;
}): Promise<AdminRelationshipPage> {
	const query = new URLSearchParams();
	for (const [key, value] of Object.entries(params)) {
		if (value !== undefined && value !== "") query.set(key, String(value));
	}

	try {
		return await serverApi<AdminRelationshipPage>(`/api/Admin/Relationships?${query}`);
	} catch (error) {
		relLogger.error("Failed to list relationships", { error });
		return { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 };
	}
}
