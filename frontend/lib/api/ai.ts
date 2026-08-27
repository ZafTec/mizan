import { clientApi } from "@/lib/api.client";
import { resolvePublicApiOrigin } from "@/lib/api-base";

export interface AiConsent {
	enabled: boolean;
	shareNutrition: boolean;
	shareTraining: boolean;
	shareBody: boolean;
	/** Whether the assistant may act, not just answer. Separate from reading. */
	allowWrites: boolean;
	writeNutrition: boolean;
	writeTraining: boolean;
	writeBody: boolean;
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

export interface AiChatMessage {
	id: string;
	fromUser: boolean;
	content: string;
	createdAt: string;
	/** A photo sent with this turn, kept so the transcript still makes sense later. */
	imageUrl?: string | null;
}

export interface AiChatThread {
	id: string;
	title: string;
	updatedAt: string;
}

export interface AiChatThreadDetail extends AiChatThread {
	messages: AiChatMessage[];
}

export interface AiChatTurn {
	threadId: string;
	title: string;
	reply: AiChatMessage;
	/** What the turn wrote, if anything. Shown, never silent. */
	performed: AiToolInvocation[];
}

export const sendAiChatMessage = (threadId: string | null, message: string) =>
	clientApi<AiChatTurn>("/api/Ai/chat", {
		method: "POST",
		body: { threadId, message },
	});

/**
 * A turn with a photo. Multipart, so it goes through fetch rather than the
 * JSON client - the same shape the food-photo upload uses.
 */
export async function sendAiChatImage(
	threadId: string | null,
	message: string,
	file: File,
): Promise<AiChatTurn> {
	const body = new FormData();
	body.append("image", file);
	body.append("message", message);
	if (threadId) body.append("threadId", threadId);

	const response = await fetch(`${resolvePublicApiOrigin()}/api/Ai/chat/image`, {
		method: "POST",
		credentials: "include",
		body,
	});

	if (!response.ok) {
		const payload = await response.json().catch(() => null);
		throw new Error(payload?.error ?? payload?.detail ?? "That photo could not be sent.");
	}

	return (await response.json()) as AiChatTurn;
}

export const listAiChatThreads = () =>
	clientApi<AiChatThread[]>("/api/Ai/threads");

export const getAiChatThread = (id: string) =>
	clientApi<AiChatThreadDetail>(`/api/Ai/threads/${id}`);

export const deleteAiChatThread = (id: string) =>
	clientApi<void>(`/api/Ai/threads/${id}`, { method: "DELETE" });

export interface AiToolInvocation {
	tool: string;
	summary: string;
	succeeded: boolean;
	error?: string | null;
}

export interface AiOnboardingTurn {
	threadId: string;
	reply: AiChatMessage;
	performed: AiToolInvocation[];
}

export interface AiToolSummary {
	name: string;
	description: string;
}

export const sendOnboardingMessage = (threadId: string | null, message: string) =>
	clientApi<AiOnboardingTurn>("/api/Ai/onboarding", {
		method: "POST",
		body: { threadId, message },
	});

export const listOnboardingTools = () =>
	clientApi<AiToolSummary[]>("/api/Ai/onboarding/tools");

/**
 * The setup conversation so far. Null when the user has never started one -
 * the endpoint answers 204 rather than 404, since not having begun is the
 * normal first case.
 */
export const getOnboardingThread = () =>
	clientApi<AiChatThreadDetail | null>("/api/Ai/onboarding");

export interface AiTrainerAnswer {
	threadId: string;
	reply: AiChatMessage;
	axesSeen: string[];
}

export const askAboutClient = (
	clientId: string,
	threadId: string | null,
	message: string,
) =>
	clientApi<AiTrainerAnswer>(`/api/Ai/clients/${clientId}/ask`, {
		method: "POST",
		body: { threadId, message },
	});
