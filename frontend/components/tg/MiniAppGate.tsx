"use client";

import Link from "next/link";
import { useMiniAppSession } from "@/lib/tg/useMiniAppSession";
import type { MiniAppSession } from "@/lib/tg/miniAppSession";
import { openExternal, telegramWebApp } from "@/lib/tg/webApp";

/** One sentence and one action for a Mini App that could not sign in. */
export function MiniAppRefusal({
  session,
}: {
  session: Exclude<MiniAppSession, { status: "signed_in" }>;
}) {
  let sentence: string;
  let action: React.ReactNode = null;
  switch (session.status) {
    case "not_linked":
      sentence = "Link your Telegram account first.";
      action = (
        <button
          type="button"
          className="btn-primary min-h-11"
          onClick={() => openExternal(session.linkUrl)}
        >
          Link account
        </button>
      );
      break;
    case "upgrade_required":
      sentence = session.message;
      action = (
        <button
          type="button"
          className="btn-primary min-h-11"
          onClick={() => openExternal(session.upgradeUrl)}
        >
          Upgrade to Pro
        </button>
      );
      break;
    case "not_in_telegram":
      sentence = "Open this page from the Telegram bot.";
      action = (
        <Link href="/today" className="btn-secondary min-h-11">
          Open Mizan
        </Link>
      );
      break;
    case "api_unavailable":
      sentence = session.message;
      action = (
        <button
          type="button"
          className="btn-secondary min-h-11"
          onClick={() => window.location.reload()}
        >
          Try again
        </button>
      );
      break;
    default: {
      sentence = session.message;
      const close = telegramWebApp()?.close;
      if (close)
        action = (
          <button
            type="button"
            className="btn-secondary min-h-11"
            onClick={() => close()}
          >
            Close
          </button>
        );
    }
  }
  return (
    <section
      className="log-page space-y-4"
      aria-labelledby="tg-refusal"
      data-testid="tg-refusal"
    >
      <p id="tg-refusal" className="text-base">
        {sentence}
      </p>
      {action}
    </section>
  );
}

/**
 * Renders its children once the Mini App has a Mizan session; until then a
 * status line, and on a refusal the reason and what to do about it.
 */
export default function MiniAppGate({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = useMiniAppSession();
  if (!session)
    return (
      <p role="status" className="log-muted">
        Signing in…
      </p>
    );
  if (session.status === "signed_in") return <>{children}</>;
  return <MiniAppRefusal session={session} />;
}
