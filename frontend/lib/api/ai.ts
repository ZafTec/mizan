import { clientApi } from "@/lib/api.client";

export interface AiConsent {
	enabled: boolean;
	shareNutrition: boolean;
	shareTraining: boolean;
	shareBody: boolean;
	updatedAt?: string | null;
}

export interface AiQuotaSnapshot {
	requestsUsed: number;
	requestLimit: number;
	tokensUsed: number;
	tokenLimit: number;
	resetsAt: string;
	plan: string;
}

export interface AiUsageDay {
	date: string;
	requests: number;
	tokens: number;
}

export interface AiUsageFeature {
	feature: string;
	requests: number;
	tokens: number;
}

export interface MyAiUsage {
	today: AiQuotaSnapshot;
	history: AiUsageDay[];
	byFeature: AiUsageFeature[];
}

export const getAiConsent = () => clientApi<AiConsent>("/api/Ai/consent");

export const updateAiConsent = (consent: Omit<AiConsent, "updatedAt">) =>
	clientApi<AiConsent>("/api/Ai/consent", { method: "PUT", body: consent });

export const getMyAiUsage = (days = 14) =>
	clientApi<MyAiUsage>(`/api/Ai/usage?days=${days}`);
