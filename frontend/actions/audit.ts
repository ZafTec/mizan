"use server";

import { getAuditLogs, GetAuditLogsResult } from "@/data/audit";
import { auth } from "@/lib/auth";
import { headers } from "next/headers";

/**
 * Server Action to fetch audit logs for the live dashboard.
 * Restricted to admin users.
 */
export async function fetchLiveAuditLogs(page: number = 1, pageSize: number = 10): Promise<GetAuditLogsResult> {
    const session = await auth.api.getSession({
        headers: await headers(),
    });

    if (!session?.user || session.user.role !== "admin") {
        throw new Error("Unauthorized");
    }

    return await getAuditLogs({ page, pageSize });
}
