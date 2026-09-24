"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import type { AdminBillingCatalog, BillingPlan, MySubscription } from "@/lib/billing";

const subscriptionLogger = logger.createModuleLogger("subscription-data");

const FREE: MySubscription = {
	plan: "free",
	status: "none",
	isPro: false,
	isLifetime: false,
	currentPeriodEnd: null,
	trialEndsAt: null,
	canceledAt: null,
	nextBilledAt: null,
	cancelsAt: null,
	planId: null,
	planName: null,
	interval: null,
	amountCents: null,
	currency: null,
	canManage: false,
	hasBillingAccount: false,
};

export async function getMySubscription(): Promise<MySubscription> {
	try {
		return await serverApi<MySubscription>("/api/Subscriptions/me");
	} catch (error) {
		subscriptionLogger.error("Failed to get subscription", { error });
		return FREE;
	}
}

/**
 * The plans on sale. Public, so it works signed out; an empty list means the
 * pricing surfaces fall back to the landing copy rather than showing nothing.
 */
export async function getBillingPlans(): Promise<BillingPlan[]> {
	try {
		return await serverApi<BillingPlan[]>("/api/Subscriptions/plans", { requireAuth: false });
	} catch (error) {
		subscriptionLogger.warn("Failed to load billing plans", { error });
		return [];
	}
}

export async function getAdminBillingCatalog(): Promise<AdminBillingCatalog | null> {
	try {
		return await serverApi<AdminBillingCatalog>("/api/admin/billing");
	} catch (error) {
		subscriptionLogger.error("Failed to load the billing catalogue", { error });
		return null;
	}
}
