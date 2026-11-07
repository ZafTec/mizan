import { clientApi } from "@/lib/api.client";
import type { components } from "@/types/api.generated";

/** Browser calls for the person's notifications. Types come from the generated OpenAPI document. */
type Schemas = components["schemas"];

export type Notification = Schemas["NotificationDto"];
export type NotificationPage = Schemas["NotificationListResult"];
export type UnreadCount = Pick<NotificationPage, "unreadCount">;

export const listNotifications = (pageSize = 50) =>
  clientApi<NotificationPage>(`/api/Notifications?page=1&pageSize=${pageSize}`);

export const unreadNotificationCount = () =>
  clientApi<UnreadCount>("/api/Notifications/unread-count");

/** Marking read is a write the person may retry, so each carries an Idempotency-Key. */
const retrySafe = () => ({ "Idempotency-Key": crypto.randomUUID() });

export const markNotificationRead = (id: string) =>
  clientApi<void>(`/api/Notifications/${id}/read`, {
    method: "POST",
    headers: retrySafe(),
  });

export const markAllNotificationsRead = () =>
  clientApi<void>("/api/Notifications/read-all", {
    method: "POST",
    headers: retrySafe(),
  });

export type NotificationPreferences = Schemas["NotificationPreferencesDto"];
export type NotificationKindPreference =
  Schemas["NotificationKindPreferenceDto"];
export type NotificationPreferencesUpdate =
  Schemas["UpdateNotificationPreferencesRequest"];

/** Which kinds reach which channel, and the quiet hours. */
export const getNotificationPreferences = () =>
  clientApi<NotificationPreferences>("/api/Notifications/preferences");

export const updateNotificationPreferences = (
  body: NotificationPreferencesUpdate,
) =>
  clientApi<NotificationPreferences>("/api/Notifications/preferences", {
    method: "PUT",
    headers: retrySafe(),
    body,
  });
