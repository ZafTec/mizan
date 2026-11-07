import { serverApi } from "@/lib/api.server";
import type { NotificationCampaign } from "./admin-notifications";

/**
 * Server-component reads for the campaign pages. They forward the session
 * cookie and answer with the generated types.
 */
export const listCampaignsServer = () =>
  serverApi<NotificationCampaign[]>("/api/admin/notification-campaigns");

export const getCampaignServer = (id: string) =>
  serverApi<NotificationCampaign>(
    `/api/admin/notification-campaigns/${encodeURIComponent(id)}`,
  );
