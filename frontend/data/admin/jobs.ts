"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";

const jobsLogger = logger.createModuleLogger("admin-jobs");

export type JobStatus =
	| "Pending"
	| "Running"
	| "Succeeded"
	| "Failed"
	| "DeadLettered";

export interface AdminJob {
	id: string;
	type: string;
	status: JobStatus;
	attempts: number;
	runAfter: string;
	lastError?: string | null;
	createdAt: string;
	startedAt?: string | null;
	completedAt?: string | null;
}

export interface AdminJobPage {
	items: AdminJob[];
	totalCount: number;
	page: number;
	pageSize: number;
	totalPages: number;
}

export interface AdminJobStats {
	pending: number;
	running: number;
	failed: number;
	deadLettered: number;
	succeeded: number;
	types: string[];
}

const EMPTY_STATS: AdminJobStats = {
	pending: 0,
	running: 0,
	failed: 0,
	deadLettered: 0,
	succeeded: 0,
	types: [],
};

export async function listAdminJobs(params: {
	page?: number;
	pageSize?: number;
	type?: string;
	status?: string;
	sortBy?: string;
	sortOrder?: string;
}): Promise<AdminJobPage> {
	const query = new URLSearchParams();
	for (const [key, value] of Object.entries(params)) {
		if (value !== undefined && value !== "") query.set(key, String(value));
	}

	try {
		return await serverApi<AdminJobPage>(`/api/Admin/Jobs?${query}`);
	} catch (error) {
		jobsLogger.error("Failed to list jobs", { error });
		return { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 };
	}
}

export async function getAdminJobStats(): Promise<AdminJobStats> {
	try {
		return await serverApi<AdminJobStats>("/api/Admin/Jobs/stats");
	} catch (error) {
		jobsLogger.error("Failed to load job stats", { error });
		return EMPTY_STATS;
	}
}
