import { NextResponse } from "next/server";
import { logger } from "@/lib/logger";

const healthLogger = logger.createModuleLogger("health-route");

export async function GET() {
    const healthStatus = {
        status: "Healthy",
        timestamp: new Date().toISOString(),
        services: {
            backend: {
                status: "Unknown",
                latencyMs: 0,
            },
        },
        system: {
            memoryUsage: process.memoryUsage(),
            uptime: process.uptime(),
            nodeVersion: process.version,
        },
    };

    // The frontend owns no database since v2; the backend's own health check
    // covers Postgres and Redis.
    const backendStart = performance.now();
    try {
        const backendUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";
        const response = await fetch(`${backendUrl}/health`, {
            signal: AbortSignal.timeout(5000), // 5 second timeout
        });
        const backendEnd = performance.now();

        if (response.ok) {
            healthStatus.services.backend.status = "Healthy";
            healthStatus.services.backend.latencyMs = Math.round(backendEnd - backendStart);
        } else {
            healthStatus.services.backend.status = "Unhealthy";
            healthStatus.status = "Unhealthy";
        }
    } catch (error) {
        healthLogger.error("Backend API health check failed", {error});
        healthStatus.services.backend.status = "Unhealthy";
        healthStatus.status = "Unhealthy";
    }

    return NextResponse.json(
        healthStatus,
        { status: healthStatus.status === "Healthy" ? 200 : 503 }
    );
}
