/**
 * The part of Telegram's `window.Telegram.WebApp` the Mini App uses. The
 * object comes from telegram-web-app.js; outside Telegram the script still
 * defines it, with an empty `initData`. See https://core.telegram.org/bots/webapps.
 */
export interface TelegramWebApp {
  /** The signed statement for the bot service. Empty outside Telegram. */
  initData: string;
  colorScheme?: "light" | "dark";
  themeParams?: Record<string, string | undefined>;
  ready(): void;
  expand(): void;
  openLink(url: string): void;
  close?(): void;
}

declare global {
  interface Window {
    Telegram?: { WebApp?: TelegramWebApp };
  }
}

export const TELEGRAM_WEB_APP_SCRIPT =
  "https://telegram.org/js/telegram-web-app.js";

export function telegramWebApp(): TelegramWebApp | null {
  if (typeof window === "undefined") return null;
  return window.Telegram?.WebApp ?? null;
}

/**
 * Opens a page outside the WebView: Telegram's in-app browser or the system
 * one. Linking an account and paying happen where the person is signed in.
 */
export function openExternal(url: string) {
  const app = telegramWebApp();
  if (app) app.openLink(url);
  else window.open(url, "_blank", "noopener,noreferrer");
}
