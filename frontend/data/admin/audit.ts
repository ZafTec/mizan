"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";

const auditLogger = logger.createModuleLogger("admin-audit");

export interface AuditLogRow {
	id: string;
	userId?: string | null;
	userEmail?: string | null;
	action: string;
	entityType: string;
	entityId: string;
	details?: string | null;
	ipAddress?: string | null;
	timestamp: string;
}

export interface AuditLogPage {
	items: AuditLogRow[];
	totalCount: number;
	page: number;
	pageSize: number;
	totalPages: number;
}

export interface AuditFilters {
	page?: number;
	pageSize?: number;
	action?: string;
	entityType?: string;
	entityId?: string;
	search?: string;
	from?: string;
	to?: string;
	sortBy?: string;
	sortOrder?: string;
}

function query(filters: AuditFilters): string {
	const params = new URLSearchParams();
	for (const [key, value] of Object.entries(filters)) {
		if (value !== undefined && value !== null && value !== "") params.set(key, String(value));
	}
	return params.toString();
}

export async function listAuditLogs(filters: AuditFilters): Promise<AuditLogPage> {
	try {
		return await serverApi<AuditLogPage>(`/api/AuditLogs?${query(filters)}`);
	} catch (error) {
		auditLogger.error("Failed to list audit logs", { error });
		return { items: [], totalCount: 0, page: 1, pageSize: 50, totalPages: 0 };
	}
}

export async function getAuditFacets(): Promise<{ actions: string[]; entityTypes: string[] }> {
	try {
		return await serverApi<{ actions: string[]; entityTypes: string[] }>("/api/AuditLogs/facets");
	} catch (error) {
		auditLogger.warn("Failed to load audit facets", { error });
		return { actions: [], entityTypes: [] };
	}
}

/** The href for the CSV of whatever is currently filtered. */
export async function auditExportPath(filters: AuditFilters): Promise<string> {
	return `/api/AuditLogs/export?${query({ ...filters, page: undefined, pageSize: undefined })}`;
}
