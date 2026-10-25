import { clientApi } from "@/lib/api.client";
import { resolvePublicApiOrigin } from "@/lib/api-base";
import type { components } from "@/types/api.generated";

/**
 * Browser calls for the assistant. Every type is the generated OpenAPI one;
 * nothing here declares a response shape of its own (docs/AI.md).
 */
type Schemas = components["schemas"];

export type AiConsent = Schemas["AiConsentDto"];
export type AiQuotaSnapshot = Schemas["AiQuotaSnapshot"];
export type AiUsageDay = Schemas["AiUsageDayDto"];
export type AiUsageFeature = Schemas["AiUsageFeatureDto"];
/** Personal usage today, with the meal planning line under `planning`. */
export type MyAiUsage = Schemas["MyAiUsageDto"];
export type AiChatMessage = Schemas["AiChatMessageDto"];
export type AiChatThread = Schemas["AiChatThreadDto"];
export type AiChatThreadDetail = Schemas["AiChatThreadDetailDto"];
/** One turn: the stored messages, what it wrote (`performed`) and its typed `cards`. */
export type AiChatTurn = Schemas["AiChatTurnDto"];
export type AiToolInvocation = Schemas["AiToolInvocation"];
export type AiOnboardingTurn = Schemas["AiOnboardingTurnDto"];
export type AiToolSummary = Schemas["AiToolSummary"];
export type AiTrainerAnswer = Schemas["AiTrainerAnswerDto"];
/** A typed card of a chat turn: `type` names the record its `data` holds. */
export type AiCard = Schemas["AiCardDto"];
export type PlanPreviewCardData = Schemas["PlanPreviewCardData"];
export type DayPlanCardData = Schemas["DayPlanCardData"];
export type CostTableCardData = Schemas["CostTableCardData"];
export type ShoppingListCardData = Schemas["ShoppingListCardData"];

export const getAiConsent = () => clientApi<AiConsent>("/api/Ai/consent");

export const updateAiConsent = (consent: Omit<AiConsent, "updatedAt">) =>
  clientApi<AiConsent>("/api/Ai/consent", { method: "PUT", body: consent });

export const getMyAiUsage = (days = 14) =>
  clientApi<MyAiUsage>(`/api/Ai/usage?days=${days}`);

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

  const response = await fetch(
    `${resolvePublicApiOrigin()}/api/Ai/chat/image`,
    {
      method: "POST",
      credentials: "include",
      body,
    },
  );

  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    throw new Error(
      payload?.error ?? payload?.detail ?? "That photo could not be sent.",
    );
  }

  return (await response.json()) as AiChatTurn;
}

export const listAiChatThreads = () =>
  clientApi<AiChatThread[]>("/api/Ai/threads");

export const getAiChatThread = (id: string) =>
  clientApi<AiChatThreadDetail>(`/api/Ai/threads/${id}`);

export const deleteAiChatThread = (id: string) =>
  clientApi<void>(`/api/Ai/threads/${id}`, { method: "DELETE" });

export const sendOnboardingMessage = (
  threadId: string | null,
  message: string,
) =>
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

export const askAboutClient = (
  clientId: string,
  threadId: string | null,
  message: string,
) =>
  clientApi<AiTrainerAnswer>(`/api/Ai/clients/${clientId}/ask`, {
    method: "POST",
    body: { threadId, message },
  });
