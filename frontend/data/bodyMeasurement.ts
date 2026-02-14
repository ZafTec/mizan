"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";

const bmLogger = logger.createModuleLogger("body-measurement-data");

export interface BodyMeasurement {
    id: string;
    date: string;
    weightKg?: number | null;
    bodyFatPercentage?: number | null;
    muscleMassKg?: number | null;
    waistCm?: number | null;
    hipsCm?: number | null;
    chestCm?: number | null;
    armsCm?: number | null;
    thighsCm?: number | null;
    notes?: string | null;
}

export interface BodyMeasurementListResult {
    bodyMeasurements: BodyMeasurement[];
    totalCount: number;
    totalPages: number;
}

export async function getBodyMeasurements(page: number = 1, pageSize: number = 20, sortBy?: string, sortOrder?: string): Promise<BodyMeasurementListResult> {
    try {
        const params = new URLSearchParams();
        params.append("Page", page.toString());
        params.append("PageSize", pageSize.toString());
        if (sortBy) params.append("SortBy", sortBy);
        if (sortOrder) params.append("SortOrder", sortOrder);

        const result = await serverApi<{ items: BodyMeasurement[], totalCount: number, page: number, pageSize: number, totalPages: number }>(`/api/BodyMeasurements?${params}`);
        return {
            bodyMeasurements: result.items || [],
            totalCount: result.totalCount || 0,
            totalPages: result.totalPages || 0
        };
    } catch (error) {
        bmLogger.error("Failed to get body measurements", { error });
        return { bodyMeasurements: [], totalCount: 0, totalPages: 0 };
    }
}

export async function deleteBodyMeasurement(id: string): Promise<boolean> {
    try {
        await serverApi(`/api/BodyMeasurements/${id}`, {
            method: "DELETE",
        });
        return true;
    } catch (error) {
        bmLogger.error("Failed to delete body measurement", { error });
        return false;
    }
}

export async function addBodyMeasurement(data: Omit<BodyMeasurement, "id">): Promise<boolean> {
    try {
        await serverApi("/api/BodyMeasurements", {
            method: "POST",
            body: data,
        });
        return true;
    } catch (error) {
        bmLogger.error("Failed to add body measurement", { error });
        return false;
    }
}
