"use client";

import { useState } from "react";
import Script from "next/script";
import { MiniAppScriptContext } from "@/lib/tg/useMiniAppSession";
import { TELEGRAM_WEB_APP_SCRIPT, telegramWebApp } from "@/lib/tg/webApp";

/**
 * The Mini App's frame: no navigation, no footer, Telegram's colours on the
 * app's tokens (app/tg/miniapp.css). It loads telegram-web-app.js, tells
 * Telegram the page is ready and asks for the full height.
 */
export default function MiniAppShell({
  children,
}: {
  children: React.ReactNode;
}) {
  const [scriptReady, setScriptReady] = useState(false);

  return (
    <MiniAppScriptContext.Provider value={scriptReady}>
      <Script
        src={TELEGRAM_WEB_APP_SCRIPT}
        strategy="afterInteractive"
        onReady={() => {
          const app = telegramWebApp();
          app?.ready();
          app?.expand();
          setScriptReady(true);
        }}
        // Outside Telegram, or with telegram.org unreachable: the page says
        // to open it from the bot instead of waiting forever.
        onError={() => setScriptReady(true)}
      />
      <div className="tg-shell">{children}</div>
    </MiniAppScriptContext.Provider>
  );
}
