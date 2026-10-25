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
  /** The bar at the bottom of the WebView. Older clients may not have it. */
  MainButton?: TelegramMainButtonApi;
}

/** Telegram's `WebApp.MainButton`, as much of it as the Mini App uses. */
export interface TelegramMainButtonApi {
  setText(text: string): unknown;
  show(): unknown;
  hide(): unknown;
  enable(): unknown;
  disable(): unknown;
  showProgress(leaveActive?: boolean): unknown;
  hideProgress(): unknown;
  onClick(handler: () => void): unknown;
  offClick(handler: () => void): unknown;
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

/** A thin typed handle on Telegram's main button. */
export interface MainButton {
  show(): void;
  hide(): void;
  setText(text: string): void;
  /** Greys the button out, or makes it pressable again. */
  setEnabled(enabled: boolean): void;
  /** A spinner on the button while its write runs; the button stays inactive. */
  showProgress(busy: boolean): void;
  /** Calls `handler` on each press; the returned function stops that. */
  onClick(handler: () => void): () => void;
}

/** The main button inside Telegram, or null in a browser or an older client. */
export function mainButton(): MainButton | null {
  const app = telegramWebApp();
  const button = app?.initData ? app.MainButton : undefined;
  if (!button) return null;
  return {
    show: () => void button.show(),
    hide: () => void button.hide(),
    setText: (text) => void button.setText(text),
    setEnabled: (enabled) =>
      void (enabled ? button.enable() : button.disable()),
    showProgress: (busy) =>
      void (busy ? button.showProgress(false) : button.hideProgress()),
    onClick: (handler) => {
      button.onClick(handler);
      return () => void button.offClick(handler);
    },
  };
}
