import { db } from "@/db/client";
import { sql } from "drizzle-orm";
import { NextResponse } from "next/server";
import { users } from "@/db/schema";

export async function GET() {
    const healthStatus = {
        status: "Healthy",
        timestamp: new Date().toISOString(),
        services: {
            database: {
                status: "Unknown",
                latencyMs: 0,
            },
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

    // Check database
    const dbStart = performance.now();
    try {
        await db.select({ id: users.id }).from(users).limit(1);
        const dbEnd = performance.now();

        healthStatus.services.database.status = "Healthy";
        healthStatus.services.database.latencyMs = Math.round(dbEnd - dbStart);
    } catch (error) {
        console.error("Database health check failed:", error);
        healthStatus.status = "Unhealthy";
        healthStatus.services.database.status = "Unhealthy";
    }

    // Check backend API
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
        console.error("Backend health check failed:", error);
        healthStatus.services.backend.status = "Unhealthy";
        healthStatus.status = "Unhealthy";
    }

    return NextResponse.json(
        healthStatus,
        { status: healthStatus.status === "Healthy" ? 200 : 503 }
    );
}
