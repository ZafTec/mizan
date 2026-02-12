"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import { auth } from "@/lib/auth";
import { headers } from "next/headers";

const auditLogger = logger.createModuleLogger("audit-client");

export interface AuditLog {
    id: string;
    userId?: string;
    userEmail?: string;
    action: string;
    entityType: string;
    entityId: string;
    details?: string;
    ipAddress?: string;
    timestamp: string;
}

export interface GetAuditLogsResult {
    logs: AuditLog[];
    totalCount: number;
    totalPages: number;
}

export async function getAuditLogs(params: {
    action?: string;
    entityType?: string;
    entityId?: string;
    userId?: string;
    page?: number;
    pageSize?: number;
    sortBy?: string;
    sortOrder?: string;
}): Promise<GetAuditLogsResult> {
    try {
        const queryParams = new URLSearchParams();
        if (params.action) queryParams.append("Action", params.action);
        if (params.entityType) queryParams.append("EntityType", params.entityType);
        if (params.entityId) queryParams.append("EntityId", params.entityId);
        if (params.userId) queryParams.append("UserId", params.userId);
        if (params.page) queryParams.append("Page", params.page.toString());
        if (params.pageSize) queryParams.append("PageSize", (params.pageSize || 20).toString());
        if (params.sortBy) queryParams.append("SortBy", params.sortBy);
        if (params.sortOrder) queryParams.append("SortOrder", params.sortOrder);

        const result = await serverApi<{ items: AuditLog[], totalCount: number, page: number, pageSize: number, totalPages: number }>(`/api/AuditLogs?${queryParams.toString()}`, {
            method: "GET",
        });

        return {
            logs: result.items || [],
            totalCount: result.totalCount || 0,
            totalPages: result.totalPages || 0
        };
    } catch (error) {
        auditLogger.error("Error fetching audit logs", {error});
        return { logs: [], totalCount: 0, totalPages: 0 };
    }
}

export async function fetchLiveAuditLogs(page: number = 1, pageSize: number = 10): Promise<GetAuditLogsResult> {
    const session = await auth.api.getSession({
        headers: await headers(),
    });

    if (!session?.user || session.user.role !== "admin") {
        throw new Error("Unauthorized");
    }

    return await getAuditLogs({ page, pageSize });
}
