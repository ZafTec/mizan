"use client";

import { createContext, useContext, useEffect, useState } from "react";
import { exchangeMiniAppSession, type MiniAppSession } from "./miniAppSession";
import { telegramWebApp } from "./webApp";

/** True once telegram-web-app.js has run (or failed to load), so `initData` can be read. */
export const MiniAppScriptContext = createContext(false);

// One exchange per opening of the Mini App. Strict mode's second effect and a
// client-side move between /tg pages reuse it instead of signing in again.
let current: {
  initData: string;
  result: Promise<MiniAppSession>;
  settled?: MiniAppSession;
} | null = null;

export function startMiniAppSession(): Promise<MiniAppSession> {
  const initData = telegramWebApp()?.initData ?? "";
  if (!initData) return Promise.resolve({ status: "not_in_telegram" });
  if (current?.initData !== initData) {
    const next: NonNullable<typeof current> = {
      initData,
      result: exchangeMiniAppSession(initData),
    };
    void next.result.then((session) => (next.settled = session));
    current = next;
  }
  return current.result;
}

/** Forgets the exchange, so the next call signs in again. */
export function resetMiniAppSession() {
  current = null;
}

/**
 * Null while the Telegram script loads and the bot service answers; then
 * `signed_in` (the `mizan_session` cookie is set) or the refusal to show.
 */
export function useMiniAppSession(): MiniAppSession | null {
  const scriptReady = useContext(MiniAppScriptContext);
  // A move between /tg pages finds the exchange settled and renders the page at once.
  const [session, setSession] = useState<MiniAppSession | null>(
    () => current?.settled ?? null,
  );

  useEffect(() => {
    if (!scriptReady) return;
    let cancelled = false;
    void startMiniAppSession().then((result) => {
      if (!cancelled) setSession(result);
    });
    return () => {
      cancelled = true;
    };
  }, [scriptReady]);

  return session;
}
