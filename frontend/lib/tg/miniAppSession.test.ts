import { describe, expect, it, vi } from "vitest";
import {
  exchangeMiniAppSession,
  MINI_APP_SESSION_PATH,
  type Fetcher,
  type MiniAppSession,
} from "./miniAppSession";

const answer = (status: number, body: unknown) =>
  vi.fn<Fetcher>(async () =>
    Response.json(body, {
      status,
      headers: { "Content-Type": "application/json" },
    }),
  );

// The bodies MiniAppSessionEndpoint writes, and what the page makes of each.
const cases: [string, number, unknown, MiniAppSession][] = [
  [
    "a session",
    200,
    { token: "t", expiresAt: "2026-10-26T08:00:00Z" },
    { status: "signed_in" },
  ],
  [
    "an unlinked chat",
    404,
    {
      errorCode: "not_linked",
      error: "This Telegram account is not connected to a Mizan account yet.",
      linkUrl: "https://mizan.test/profile/settings/telegram",
    },
    {
      status: "not_linked",
      message: "This Telegram account is not connected to a Mizan account yet.",
      linkUrl: "https://mizan.test/profile/settings/telegram",
    },
  ],
  [
    "a Free account",
    402,
    {
      errorCode: "upgrade_required",
      error: "The Telegram bot is part of Mizan Pro.",
      upgradeUrl: "https://mizan.test/billing",
    },
    {
      status: "upgrade_required",
      message: "The Telegram bot is part of Mizan Pro.",
      upgradeUrl: "https://mizan.test/billing",
    },
  ],
  [
    "stale initData",
    401,
    {
      errorCode: "invalid_init_data",
      error: "initData has expired; open the Mini App again.",
    },
    {
      status: "invalid_init_data",
      message: "initData has expired; open the Mini App again.",
    },
  ],
  [
    "a body the bot could not read",
    400,
    { errorCode: "bad_request", error: "Send { initData }." },
    { status: "invalid_init_data", message: "Send { initData }." },
  ],
  [
    "a banned account",
    403,
    { errorCode: "forbidden", error: "This account is suspended." },
    { status: "forbidden", message: "This account is suspended." },
  ],
  [
    "a locked account",
    429,
    { errorCode: "account_locked", error: "Too many attempts." },
    { status: "account_locked", message: "Too many attempts." },
  ],
  [
    "the API down",
    502,
    {
      errorCode: "api_unavailable",
      error: "Mizan is not reachable right now. Try again in a moment.",
    },
    {
      status: "api_unavailable",
      message: "Mizan is not reachable right now. Try again in a moment.",
    },
  ],
  [
    "a proxy error page",
    504,
    "<html>Gateway Timeout</html>",
    {
      status: "api_unavailable",
      message: "Mizan is not reachable right now. Try again in a moment.",
    },
  ],
  [
    "a 404 from something other than the bot",
    404,
    { title: "Not Found" },
    {
      status: "api_unavailable",
      message: "Mizan is not reachable right now. Try again in a moment.",
    },
  ],
  [
    "a locked account without a reason",
    429,
    { errorCode: "account_locked" },
    {
      status: "account_locked",
      message: "This account cannot sign in right now.",
    },
  ],
];

describe("the Mini App session exchange", () => {
  it.each(cases)("reads %s", async (_, status, body, expected) => {
    expect(
      await exchangeMiniAppSession("query_id=1&hash=h", answer(status, body)),
    ).toEqual(expected);
  });

  it("sends only initData, to the app's own host, without a bearer token", async () => {
    const fetcher = answer(200, { token: "t", expiresAt: "x" });
    await exchangeMiniAppSession("query_id=1&hash=h", fetcher);
    expect(fetcher).toHaveBeenCalledTimes(1);
    const [path, init] = fetcher.mock.calls[0];
    expect(path).toBe(MINI_APP_SESSION_PATH);
    expect(init?.method).toBe("POST");
    expect(init?.credentials).toBe("same-origin");
    expect(JSON.parse(String(init?.body))).toEqual({
      initData: "query_id=1&hash=h",
    });
    expect(
      Object.keys(init?.headers ?? {}).map((key) => key.toLowerCase()),
    ).not.toContain("authorization");
  });

  it("does not call the bot outside Telegram", async () => {
    const fetcher = answer(200, {});
    expect(await exchangeMiniAppSession("", fetcher)).toEqual({
      status: "not_in_telegram",
    });
    expect(fetcher).not.toHaveBeenCalled();
  });

  it("treats a network failure as the service being unreachable", async () => {
    const fetcher = vi.fn<Fetcher>(async () => {
      throw new TypeError("Failed to fetch");
    });
    expect(await exchangeMiniAppSession("hash=h", fetcher)).toEqual({
      status: "api_unavailable",
      message: "Mizan is not reachable right now. Try again in a moment.",
    });
  });
});
