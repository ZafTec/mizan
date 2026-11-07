/**
 * The Mini App's sign-in: Telegram's signed `initData` goes to the bot
 * service's `POST /telegram/miniapp/session`, which answers with the
 * `mizan_session` cookie or a refusal (docs/TELEGRAM.md#mini-app). The bot
 * service is not in the API's OpenAPI document, so its answer is read here
 * by `errorCode`; the records are `MiniAppSessionRequest` and
 * `MiniAppSessionResponse` in Mizan.Contracts, and the refusal bodies are
 * written by `MiniAppSessionEndpoint`.
 */
export const MINI_APP_SESSION_PATH = "/telegram/miniapp/session";

export type MiniAppRefusalCode =
  "invalid_init_data" | "forbidden" | "account_locked" | "api_unavailable";

export type MiniAppSession =
  | { status: "signed_in" }
  | { status: "not_linked"; message: string; linkUrl: string }
  | { status: "upgrade_required"; message: string; upgradeUrl: string }
  | { status: MiniAppRefusalCode; message: string }
  | { status: "not_in_telegram" };

const UNAVAILABLE = "Mizan is not reachable right now. Try again in a moment.";
const LOCKED = "This account cannot sign in right now.";

/** Used only when the answer carries no `error` of its own. */
const FALLBACK: Record<MiniAppRefusalCode, string> = {
  invalid_init_data:
    "Telegram's sign-in did not check out. Open the Mini App again.",
  forbidden: LOCKED,
  account_locked: LOCKED,
  api_unavailable: UNAVAILABLE,
};

const BY_STATUS: Record<number, MiniAppRefusalCode> = {
  400: "invalid_init_data",
  401: "invalid_init_data",
  403: "forbidden",
  429: "account_locked",
};

function text(body: Record<string, unknown>, key: string): string | null {
  const value = body[key];
  return typeof value === "string" && value.trim() ? value : null;
}

/** What the page shows for one answer from the bot service. */
export function readSessionAnswer(
  status: number,
  body: unknown,
): MiniAppSession {
  if (status >= 200 && status < 300) return { status: "signed_in" };
  const fields =
    body && typeof body === "object" ? (body as Record<string, unknown>) : {};
  const code = text(fields, "errorCode");
  const message = text(fields, "error");
  const linkUrl = text(fields, "linkUrl");
  const upgradeUrl = text(fields, "upgradeUrl");

  if ((code === "not_linked" || status === 404) && linkUrl)
    return {
      status: "not_linked",
      message: message ?? "Link your Telegram account first.",
      linkUrl,
    };
  if ((code === "upgrade_required" || status === 402) && upgradeUrl)
    return {
      status: "upgrade_required",
      message: message ?? "The Telegram bot is part of Mizan Pro.",
      upgradeUrl,
    };

  const refusal =
    (Object.keys(FALLBACK) as MiniAppRefusalCode[]).find(
      (candidate) => candidate === code,
    ) ??
    BY_STATUS[status] ??
    "api_unavailable";
  return { status: refusal, message: message ?? FALLBACK[refusal] };
}

/**
 * One exchange. Same origin, so the answer's Set-Cookie lands on the app's
 * host; the body carries only `initData`, and no Authorization header is sent.
 */
export type Fetcher = (path: string, init: RequestInit) => Promise<Response>;

export async function exchangeMiniAppSession(
  initData: string,
  fetcher: Fetcher = (path, init) => fetch(path, init),
): Promise<MiniAppSession> {
  if (!initData) return { status: "not_in_telegram" };
  try {
    const response = await fetcher(MINI_APP_SESSION_PATH, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ initData }),
    });
    const body: unknown = await response.json().catch(() => null);
    return readSessionAnswer(response.status, body);
  } catch {
    return { status: "api_unavailable", message: UNAVAILABLE };
  }
}
