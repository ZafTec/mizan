import { ApiError } from "@/lib/api";
import type { components } from "@/types/api.generated";

export type BillingPlan = components["schemas"]["BillingPlanDto"];
export type BillingDeal = components["schemas"]["BillingDealDto"];
export type MySubscription = components["schemas"]["MySubscriptionDto"];
export type PlanChangePreview = components["schemas"]["PlanChangePreviewDto"];
export type BillingTransaction = components["schemas"]["BillingTransactionDto"];
export type AdminBillingCatalog = components["schemas"]["AdminBillingCatalogDto"];
export type AdminBillingPlan = components["schemas"]["AdminBillingPlanDto"];
export type AdminBillingDiscount = components["schemas"]["AdminBillingDiscountDto"];

/** Amounts arrive in the currency's lowest unit. USD is the only currency sold. */
export function formatMoney(cents: number, currency = "USD"): string {
	return new Intl.NumberFormat("en-US", {
		style: "currency",
		currency,
		minimumFractionDigits: cents % 100 === 0 ? 0 : 2,
	}).format(cents / 100);
}

export function cadence(interval: string | null | undefined): string {
	return interval === "year" ? "/yr" : "/mo";
}

/** "25% off" or "$1 off", plus how long it lasts. */
export function describeDeal(deal: Pick<BillingDeal, "type" | "amount" | "recurring" | "maximumRecurringIntervals">, interval?: string): string {
	const off = deal.type === "percentage" ? `${Number(deal.amount)}% off` : `${formatMoney(Number(deal.amount))} off`;
	if (!deal.recurring) return `${off} your first payment`;
	if (deal.maximumRecurringIntervals) {
		const unit = interval === "year" ? "year" : "month";
		const n = deal.maximumRecurringIntervals;
		return `${off} for ${n} ${unit}${n === 1 ? "" : "s"}`;
	}
	return `${off} every renewal`;
}

/** The monthly and yearly plans on sale, cheapest first within each interval. */
export function pickPlans(plans: BillingPlan[]): { monthly?: BillingPlan; yearly?: BillingPlan } {
	return {
		monthly: plans.find((p) => p.interval === "month"),
		yearly: plans.find((p) => p.interval === "year"),
	};
}

/** What a year costs on the monthly plan versus the yearly one, as a whole percentage. */
export function yearlySavingPercent(monthly?: BillingPlan, yearly?: BillingPlan): number | null {
	if (!monthly || !yearly) return null;
	const saving = 1 - yearly.amountCents / (monthly.amountCents * 12);
	return saving > 0.01 ? Math.round(saving * 100) : null;
}

/** Pro gates answer 402 upgrade_required; the UI opens the upgrade path on it. */
export function isUpgradeRequired(error: unknown): boolean {
	return error instanceof ApiError && error.status === 402;
}

/** Validation errors arrive as a list; the first message is the one worth showing. */
export function firstErrorMessage(error: unknown, fallback: string): string {
	if (error instanceof ApiError && error.body && typeof error.body === "object") {
		const body = error.body as { errors?: { errorMessage?: string }[]; error?: string };
		if (body.errors?.[0]?.errorMessage) return body.errors[0].errorMessage;
		if (typeof body.error === "string") return body.error;
	}
	return error instanceof Error && !(error instanceof ApiError) ? error.message : fallback;
}
