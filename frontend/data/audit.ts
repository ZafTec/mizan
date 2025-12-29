import { callBackendApi } from "@/lib/backend-api-client";
import { logger } from "@/lib/logger";

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
}

/**
 * Fetch audit logs from the backend API
 */
export async function getAuditLogs(params: {
    action?: string;
    entityType?: string;
    entityId?: string;
    userId?: string;
    page?: number;
    pageSize?: number;
}): Promise<GetAuditLogsResult> {
    try {
        const queryParams = new URLSearchParams();
        if (params.action) queryParams.append("Action", params.action);
        if (params.entityType) queryParams.append("EntityType", params.entityType);
        if (params.entityId) queryParams.append("EntityId", params.entityId);
        if (params.userId) queryParams.append("UserId", params.userId);
        if (params.page) queryParams.append("Page", params.page.toString());
        if (params.pageSize) queryParams.append("PageSize", (params.pageSize || 20).toString());

        const result = await callBackendApi<GetAuditLogsResult>(`/api/AuditLogs?${queryParams.toString()}`, {
            method: "GET",
        });

        return result;
    } catch (error) {
        auditLogger.error("Error fetching audit logs", {error});
        return { logs: [], totalCount: 0 };
    }
}
