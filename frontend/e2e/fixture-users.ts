import type { components } from "../types/api.generated";
import type { PricesState } from "./fixture-prices";

/**
 * The viewer's profile and Telegram link in the UI fixture: `/api/Users/me`
 * and `/api/Telegram/link`, typed from the generated OpenAPI schema. The
 * profile carries the prices fixture's region and the Telegram switch; the
 * rules are the API's: turning `notifyByTelegram` on without a linked chat is
 * a 400, and disconnecting turns it off. Every write lands in `writes[]` with
 * its Idempotency-Key.
 */
type Schemas = components["schemas"];

export type TelegramState = {
  configured: boolean;
  linked: boolean;
  notifyByTelegram: boolean;
};

export type TelegramOptions = Partial<TelegramState>;

export function initialTelegram(options: TelegramOptions = {}): TelegramState {
  return {
    configured: options.configured ?? true,
    linked: options.linked ?? false,
    notifyByTelegram: (options.linked ?? false) && !!options.notifyByTelegram,
  };
}

type Json = (data: unknown, status?: number) => Response;
export type UsersHost = {
  user: { id: string; email: string; name: string; image: string | null };
  prices: PricesState;
  telegram: TelegramState;
  writes: { path: string; body: Record<string, unknown>; key?: string }[];
};

/** Answers the profile and Telegram link routes, or null for any other path. */
export async function handleUsers(
  request: Request,
  url: URL,
  host: UsersHost,
  json: Json,
): Promise<Response | null> {
  const { prices, telegram, writes } = host;
  const path = url.pathname;
  const method = request.method;
  const record = (data: Record<string, unknown>) =>
    writes.push({
      path,
      body: data,
      key: request.headers.get("Idempotency-Key") ?? undefined,
    });
  const invalid = (error: string) =>
    json({ errorCode: "domain_validation_failed", error }, 400);

  if (path === "/api/Users/me" && method === "GET")
    return json({
      id: host.user.id,
      email: host.user.email,
      name: host.user.name,
      image: host.user.image,
      createdAt: "2026-01-04T08:00:00Z",
      currentGoal: undefined,
      streakCount: 4,
      priceRegionCode: prices.regionCode,
      notifyByTelegram: telegram.notifyByTelegram,
    } satisfies Schemas["UserDto"]);
  if (path === "/api/Users/me" && method === "PUT") {
    const text = await request.text();
    const input = (
      text ? JSON.parse(text) : {}
    ) as Schemas["UpdateUserRequest"];
    record(input as Record<string, unknown>);
    if (input.priceRegionCode != null) {
      const region = prices.regions.find(
        (candidate) => candidate.code === input.priceRegionCode,
      );
      if (!region?.enabled)
        return invalid("That price region does not exist or is disabled.");
      prices.regionCode = region.code;
    }
    if (input.notifyByTelegram === true && !telegram.linked)
      return invalid(
        "Connect a Telegram chat before turning on Telegram notifications.",
      );
    if (input.notifyByTelegram != null)
      telegram.notifyByTelegram = input.notifyByTelegram;
    return json(null, 204);
  }

  if (path === "/api/Telegram/link" && method === "GET")
    return json({
      linked: telegram.linked,
      telegramUsername: telegram.linked ? "alex_m" : null,
      linkedAt: telegram.linked ? "2026-10-01T08:00:00Z" : null,
      lastSeenAt: null,
      botUsername: "mizan_test_bot",
      botConfigured: telegram.configured,
    } satisfies Schemas["TelegramLinkDto"]);
  if (path === "/api/Telegram/link" && method === "DELETE") {
    record({});
    telegram.linked = false;
    telegram.notifyByTelegram = false;
    return json(null, 204);
  }

  return null;
}
