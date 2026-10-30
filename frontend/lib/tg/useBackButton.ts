"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { NAV_BACK } from "@/components/motion/DirectionalTransition";
import { telegramWebApp } from "./webApp";

/**
 * Telegram's back arrow, while a page the Mini App home opened is showing. It goes
 * home as a NAV_BACK navigation, so the page slides the way it came; the system back
 * gesture carries no direction. Outside Telegram this does nothing.
 */
export function useBackButton(href = "/tg") {
  const router = useRouter();
  useEffect(() => {
    const app = telegramWebApp();
    const button = app?.initData ? app.BackButton : undefined;
    if (!button) return;
    const back = () => router.push(href, { transitionTypes: NAV_BACK });
    button.onClick(back);
    button.show();
    return () => {
      button.offClick(back);
      button.hide();
    };
  }, [href, router]);
}
