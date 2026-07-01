"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import type { MySubscription } from "@/types/subscription";

const subscriptionLogger = logger.createModuleLogger("subscription-data");

export async function getMySubscription(): Promise<MySubscription> {
    try {
        return await serverApi<MySubscription>("/api/Subscriptions/me");
    } catch (error) {
        subscriptionLogger.error("Failed to get subscription", { error });
        return { plan: "free", status: "none", isPro: false, isLifetime: false, currentPeriodEnd: null, trialEndsAt: null, canceledAt: null };
    }
}
