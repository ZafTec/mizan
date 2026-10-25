import type { NextRequest } from "next/server";

/**
 * POST /telegram/miniapp/session on the app's own host, for the Mini App page.
 *
 * It hands the body to the bot service, which verifies Telegram's signature
 * and mints the session, and relays the answer as it came: the status, the
 * JSON body and the `mizan_session` Set-Cookie. Nothing here reads initData,
 * logs it or holds the bot token. In production the reverse proxy routes this
 * path to the bot container directly (docs/ARCHITECTURE.md#request-boundaries),
 * and this handler is never reached.
 */
export const dynamic = "force-dynamic";

function botServiceUrl(): string {
  // Server-side only. The Compose service name and port of the bot container.
  return (process.env.TELEGRAM_BOT_URL || "http://telegram:5002").replace(
    /\/$/,
    "",
  );
}

const UNAVAILABLE = {
  errorCode: "api_unavailable",
  error: "Mizan is not reachable right now. Try again in a moment.",
};

export async function POST(request: NextRequest) {
  const headers: Record<string, string> = {
    "Content-Type": request.headers.get("content-type") ?? "application/json",
  };
  // The bot records these on the session row, as a browser sign-in would.
  const userAgent = request.headers.get("user-agent");
  if (userAgent) headers["User-Agent"] = userAgent;
  const forwardedFor = request.headers.get("x-forwarded-for");
  if (forwardedFor) headers["X-Forwarded-For"] = forwardedFor;

  let answer: Response;
  try {
    answer = await fetch(`${botServiceUrl()}/telegram/miniapp/session`, {
      method: "POST",
      headers,
      body: await request.text(),
      cache: "no-store",
      redirect: "manual",
    });
  } catch {
    return Response.json(UNAVAILABLE, { status: 502 });
  }

  const relayed = new Headers({
    "Content-Type": answer.headers.get("content-type") ?? "application/json",
    "Cache-Control": "no-store",
  });
  for (const cookie of answer.headers.getSetCookie())
    relayed.append("Set-Cookie", cookie);

  return new Response(await answer.text(), {
    status: answer.status,
    headers: relayed,
  });
}
