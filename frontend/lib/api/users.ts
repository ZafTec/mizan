import { clientApi } from "@/lib/api.client";
import type { components } from "@/types/api.generated";

/**
 * Browser calls for the viewer's own profile, `/api/Users/me`. Types come
 * from the generated OpenAPI document. A write a person may retry carries an
 * Idempotency-Key, like mealPlans.ts and prices.ts.
 */
type Schemas = components["schemas"];

export type UserProfile = Schemas["UserDto"];

function idempotent(): Record<string, string> {
  return { "Idempotency-Key": crypto.randomUUID() };
}

/** The viewer's profile: `priceRegionCode`, `notifyByTelegram` and the rest. */
export const getMyProfile = () => clientApi<UserProfile>("/api/Users/me");

/**
 * Sends notifications to the linked Telegram chat as well, or stops. The API
 * refuses `true` with 400 while no chat is linked; `false` always works.
 */
export const setNotifyByTelegram = (notifyByTelegram: boolean) =>
  clientApi<void>("/api/Users/me", {
    method: "PUT",
    body: { notifyByTelegram } satisfies Schemas["UpdateUserRequest"],
    headers: idempotent(),
  });
