/**
 * The Telegram half of the UI fixture: the bot service's
 * `POST /telegram/miniapp/session`, which the Next route handler forwards to
 * (TELEGRAM_BOT_URL points here in playwright.config.ts). The bodies and
 * status codes are the ones `MiniAppSessionEndpoint` answers with.
 *
 * A spec registers an opening with `POST /__fixture/telegram` and gets back
 * the `initData` Telegram would hand the page, plus the fixture session's
 * token. The outcome is chosen per opening; on `signed_in` the exchange sets
 * that token as the `mizan_session` cookie. Every opening has a fixture
 * session behind it, so its state (and `unhandled`) can be read even when
 * the browser never got the cookie.
 */
export type TelegramOutcome =
  "signed_in" | "not_linked" | "upgrade_required" | "invalid_init_data";

export type TelegramOpening = {
  outcome: TelegramOutcome;
  token: string;
  /** One entry per exchange, with the headers that must never arrive. */
  exchanges: { initData: string; authorization: string | null }[];
};

type Json = (data: unknown, status?: number, headers?: HeadersInit) => Response;
type TelegramHost = {
  /** This fixture's own origin, to create the session behind an opening. */
  self: string;
  /** The app's public origin, for linkUrl and upgradeUrl. */
  webOrigin: string;
};

const openings = new Map<string, TelegramOpening>();

function initDataFor(hash: string) {
  const user = { id: 424242, first_name: "Alex", username: "alex_m" };
  return new URLSearchParams({
    query_id: "AAHdF6IQAAAAAN0XohDhrOrc",
    user: JSON.stringify(user),
    auth_date: String(Math.floor(Date.now() / 1000)),
    hash,
  }).toString();
}

export async function handleTelegram(
  request: Request,
  url: URL,
  host: TelegramHost,
  json: Json,
): Promise<Response | null> {
  const path = url.pathname;
  const method = request.method;

  if (path === "/__fixture/telegram" && method === "POST") {
    const options = (await request.json()) as {
      outcome?: TelegramOutcome;
      session?: Record<string, unknown>;
    };
    const created = await fetch(`${host.self}/__fixture/session`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(options.session ?? {}),
    });
    const token = created.headers
      .get("set-cookie")
      ?.match(/mizan_session=([^;]+)/)?.[1];
    if (!token) return json({ error: "No fixture session" }, 500);
    const hash = crypto.randomUUID().replaceAll("-", "");
    openings.set(hash, {
      outcome: options.outcome ?? "signed_in",
      token,
      exchanges: [],
    });
    return json({ initData: initDataFor(hash), token });
  }

  if (path === "/__fixture/telegram" && method === "GET") {
    const opening = openings.get(url.searchParams.get("hash") ?? "");
    return opening ? json(opening) : json({ error: "No such opening" }, 404);
  }

  if (path !== "/telegram/miniapp/session") return null;
  if (method !== "POST") return json({ error: "Method not allowed" }, 405);

  const body = (await request.json().catch(() => null)) as {
    initData?: unknown;
  } | null;
  if (!body || typeof body !== "object")
    return json({ errorCode: "bad_request", error: "Send { initData }." }, 400);
  const initData = typeof body.initData === "string" ? body.initData : "";
  const opening = openings.get(new URLSearchParams(initData).get("hash") ?? "");
  if (!opening)
    return json(
      {
        errorCode: "invalid_init_data",
        error: "initData is not signed by this bot.",
      },
      401,
    );
  opening.exchanges.push({
    initData,
    authorization: request.headers.get("authorization"),
  });

  switch (opening.outcome) {
    case "signed_in": {
      const expiresAt = new Date(Date.now() + 24 * 3600_000);
      return json({ token: opening.token, expiresAt }, 200, {
        "Set-Cookie": `mizan_session=${opening.token}; Path=/; HttpOnly; SameSite=Lax; Expires=${expiresAt.toUTCString()}`,
      });
    }
    case "not_linked":
      return json(
        {
          errorCode: "not_linked",
          error:
            "This Telegram account is not connected to a Mizan account yet.",
          linkUrl: `${host.webOrigin}/profile/settings/telegram`,
        },
        404,
      );
    case "upgrade_required":
      return json(
        {
          errorCode: "upgrade_required",
          error: "The Telegram bot is part of Mizan Pro.",
          upgradeUrl: `${host.webOrigin}/billing`,
        },
        402,
      );
    case "invalid_init_data":
      return json(
        {
          errorCode: "invalid_init_data",
          error: "initData has expired; open the Mini App again.",
        },
        401,
      );
  }
}
