import { clientApi } from "@/lib/api.client";
import type { components } from "@/types/api.generated";

/**
 * Browser calls for notification campaigns and lifecycle messages
 * (docs/ADMIN.md#notification-campaigns). The API resolves audiences, renders
 * previews, checks the confirmed count and keeps the report; these only send
 * what the administrator chose. Every write carries an Idempotency-Key, so a
 * retried send or start reaches nobody twice.
 */
type Schemas = components["schemas"];

export type NotificationCampaign = Schemas["NotificationCampaignDto"];
export type SaveCampaignRequest = Schemas["SaveNotificationCampaignRequest"];
export type CampaignAudienceFilter = Schemas["CampaignAudienceFilter"];
export type CampaignTiming = Schemas["CampaignTiming"];
export type CampaignContent = Schemas["CampaignContent"];
export type CampaignAudiencePreview = Schemas["CampaignAudiencePreviewDto"];
export type CampaignPreview = Schemas["CampaignPreviewDto"];
export type CampaignChannelPreview = Schemas["CampaignChannelPreview"];
export type CampaignChannelReport = Schemas["CampaignChannelReport"];
export type CampaignTestResult = Schemas["CampaignTestResultDto"];
export type MarketingOptOut = Schemas["MarketingOptOutDto"];

const BASE = "/api/admin/notification-campaigns";

function idempotent(): Record<string, string> {
  return { "Idempotency-Key": crypto.randomUUID() };
}

export const createCampaign = (request: SaveCampaignRequest) =>
  clientApi<NotificationCampaign>(BASE, {
    method: "POST",
    body: request,
    headers: idempotent(),
  });

export const updateCampaign = (id: string, request: SaveCampaignRequest) =>
  clientApi<NotificationCampaign>(`${BASE}/${id}`, {
    method: "PUT",
    body: request,
    headers: idempotent(),
  });

export const previewCampaignAudience = (
  request: Schemas["CampaignAudienceRequest"],
) =>
  clientApi<CampaignAudiencePreview>(`${BASE}/audience`, {
    method: "POST",
    body: request,
  });

export const previewCampaignMessage = (
  request: Schemas["CampaignPreviewRequest"],
) =>
  clientApi<CampaignPreview>(`${BASE}/preview`, {
    method: "POST",
    body: request,
  });

export const getCampaign = (id: string) =>
  clientApi<NotificationCampaign>(`${BASE}/${id}`);

export const sendCampaignTest = (id: string) =>
  clientApi<CampaignTestResult>(`${BASE}/${id}/test`, {
    method: "POST",
    headers: idempotent(),
  });

/** `audienceCount` is the count the administrator confirmed; the API answers 409 when it has changed. */
export const activateCampaign = (id: string, audienceCount: number) =>
  clientApi<NotificationCampaign>(`${BASE}/${id}/activate`, {
    method: "POST",
    body: {
      audienceCount,
    } satisfies Schemas["ActivateNotificationCampaignRequest"],
    headers: idempotent(),
  });

export type CampaignStep = "pause" | "resume" | "cancel";

export const changeCampaign = (id: string, step: CampaignStep) =>
  clientApi<NotificationCampaign>(`${BASE}/${id}/${step}`, {
    method: "POST",
    headers: idempotent(),
  });

/** The one-tap opt-out from a marketing email's link. No sign-in needed. */
export const optOutOfMarketing = (token: string) =>
  clientApi<MarketingOptOut>("/api/Notifications/opt-out", {
    method: "POST",
    body: { token } satisfies Schemas["MarketingOptOutRequest"],
    headers: idempotent(),
  });
